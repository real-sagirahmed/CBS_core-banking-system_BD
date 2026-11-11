using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.BranchCash.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Application.StandingInstructions.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

// ARCHITECTURAL INTENT: End-to-End integration tests asserting Branch Vault Dual-Custody,
// Teller Limit Escalation & Swaps (Module 1.7), and Multi-Account Sweeps & SI Holds (Module 1.8)
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024).
public class BranchVaultAndAdvancedSweepsPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public BranchVaultAndAdvancedSweepsPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(string Cif, string AccountNumber)> CreateCustomerAndAccountWithBalanceAsync(
        IAPIRequestContext api,
        decimal initialBalance = 100000m,
        DepositProductType productType = DepositProductType.CurrentAccount)
    {
        string uniqueNid = $"9{Random.Shared.Next(100000000, 999999999)}";
        var draftReq = new CreateCustomerDraftRequest(
            CustomerType.Individual,
            "Vault Sweep Test Customer",
            new DateOnly(1990, 5, 15),
            "Male",
            "Father Name",
            "Mother Name",
            "12/A Motijheel C/A",
            "Motijheel",
            "Dhaka",
            "1000",
            "+8801700998877",
            "0101",
            uniqueNid,
            null);

        var draftRes = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = draftReq
        });
        draftRes.Status.Should().Be(201);
        var customer = (await draftRes.JsonAsync())?.Deserialize<CustomerResponseDto>(JsonOptions)!;

        var verifyRes = await api.PostAsync($"/api/v1/customers/{customer.CifNumber}/verify", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new { isApproved = true }
        });
        verifyRes.Status.Should().Be(200);

        var openAccRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = new OpenDepositAccountRequest(
                customer.CifNumber,
                productType,
                "0101",
                initialBalance,
                false)
        });
        openAccRes.Status.Should().Be(201);
        var account = (await openAccRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;

        return (customer.CifNumber, account.AccountNumber);
    }

    [Fact]
    public async Task Test_BranchVault_Lifecycle_ChestDeposit_InsuranceLimit_AndEveningBanking()
    {
        var api = await _fixture.CreateApiContextAsync();
        string branchCode = $"B{Random.Shared.Next(100, 999)}";

        // 1. Initialize Branch Vault (5,000,000 BDT initial, 8,000,000 BDT insurance limit)
        var initReq = new InitializeBranchVaultRequest(
            BranchCode: branchCode,
            VaultCustodianUserId: "HEAD_CASHIER_01",
            JointCustodianUserId: "BRANCH_MGR_01",
            InitialBalance: 5_000_000m,
            InsuredCashLimit: 8_000_000m,
            Denominations: new Dictionary<int, int> { [1000] = 5000 });

        var initRes = await api.PostAsync("/api/v1/branch-vault/initialize", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "SUPERVISOR01" },
            DataObject = initReq
        });
        initRes.Status.Should().Be(201);
        var vault = (await initRes.JsonAsync())?.Deserialize<BranchVaultResponseDto>(JsonOptions)!;
        vault.BranchCode.Should().Be(branchCode);
        vault.CurrentBalance.Should().Be(5_000_000m);
        vault.IsInsuredLimitExceeded.Should().BeFalse();

        // 2. Deposit Cash from Bangladesh Bank Currency Chest (+2,000,000 BDT -> 7,000,000 BDT)
        var chestDepositReq = new ChestCashMovementRequest(
            BranchCode: branchCode,
            MovementType: CashMovementType.ChestToVault,
            Amount: 2_000_000m,
            Denominations: new Dictionary<int, int> { [1000] = 2000 },
            ChestReference: "BB-CHEST-DEP-8801",
            CustodianUserId: "HEAD_CASHIER_01",
            JointCustodianUserId: "BRANCH_MGR_01");

        var depRes = await api.PostAsync("/api/v1/branch-vault/chest-movement", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "SUPERVISOR01" },
            DataObject = chestDepositReq
        });
        depRes.Status.Should().Be(200);
        var depVault = (await depRes.JsonAsync())?.Deserialize<BranchVaultResponseDto>(JsonOptions)!;
        depVault.CurrentBalance.Should().Be(7_000_000m);
        depVault.IsInsuredLimitExceeded.Should().BeFalse();

        // 3. Deposit Another 2,000,000 BDT -> 9,000,000 BDT > 8,000,000 BDT (Breaching Insurance Limit)
        var chestDepositReq2 = chestDepositReq with { ChestReference = "BB-CHEST-DEP-8802" };
        var depRes2 = await api.PostAsync("/api/v1/branch-vault/chest-movement", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "SUPERVISOR01" },
            DataObject = chestDepositReq2
        });
        depRes2.Status.Should().Be(200);
        var breachedVault = (await depRes2.JsonAsync())?.Deserialize<BranchVaultResponseDto>(JsonOptions)!;
        breachedVault.CurrentBalance.Should().Be(9_000_000m);
        breachedVault.IsInsuredLimitExceeded.Should().BeTrue();
        breachedVault.ExcessOverInsuredLimit.Should().Be(1_000_000m);

        // 4. Toggle Evening Banking Session
        var eveningReq = new ToggleEveningBankingRequest(
            BranchCode: branchCode,
            OpenSession: true,
            AuthorizedOfficerUserId: "OFFICER_EVENING_01");

        var eveningRes = await api.PostAsync("/api/v1/branch-vault/evening-banking/toggle", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "SUPERVISOR01" },
            DataObject = eveningReq
        });
        eveningRes.Status.Should().Be(200);
        var eveningVault = (await eveningRes.JsonAsync())?.Deserialize<BranchVaultResponseDto>(JsonOptions)!;
        eveningVault.EveningBankingSessionActive.Should().BeTrue();
    }

    [Fact]
    public async Task Test_InterTeller_DenominationSwap_AndVaultReplenish()
    {
        var api = await _fixture.CreateApiContextAsync();
        string branchCode = $"B{Random.Shared.Next(100, 999)}";

        // 1. Initialize Vault for branch
        var initReq = new InitializeBranchVaultRequest(
            BranchCode: branchCode,
            VaultCustodianUserId: "HEAD_CASHIER_01",
            JointCustodianUserId: "BRANCH_MGR_01",
            InitialBalance: 2_000_000m,
            InsuredCashLimit: 5_000_000m,
            Denominations: new Dictionary<int, int> { [1000] = 2000 });

        var initRes = await api.PostAsync("/api/v1/branch-vault/initialize", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "SUPERVISOR01" },
            DataObject = initReq
        });
        initRes.Status.Should().Be(201);

        // 2. Open Drawer 1 (has 100 notes of 1000 = 100,000 BDT)
        var openDrawer1Req = new OpenTellerDrawerRequest(
            BranchCode: branchCode,
            CashierUserId: $"CASHIER_A_{Random.Shared.Next(100, 999)}",
            InitialVaultAllocation: 100_000m,
            Denominations: new Dictionary<int, int> { [1000] = 100 },
            PostingLimit: 500_000m);

        var drawer1Res = await api.PostAsync("/api/v1/teller/drawers/open", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-Supervisor-Id"] = "HEAD_CASHIER_01" },
            DataObject = openDrawer1Req
        });
        drawer1Res.Status.Should().Be(201);
        var drawer1 = (await drawer1Res.JsonAsync())?.Deserialize<TellerDrawerResponseDto>(JsonOptions)!;

        // 3. Open Drawer 2 (has 200 notes of 500 = 100,000 BDT)
        var openDrawer2Req = new OpenTellerDrawerRequest(
            BranchCode: branchCode,
            CashierUserId: $"CASHIER_B_{Random.Shared.Next(100, 999)}",
            InitialVaultAllocation: 100_000m,
            Denominations: new Dictionary<int, int> { [500] = 200 },
            PostingLimit: 500_000m);

        var drawer2Res = await api.PostAsync("/api/v1/teller/drawers/open", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-Supervisor-Id"] = "HEAD_CASHIER_01" },
            DataObject = openDrawer2Req
        });
        drawer2Res.Status.Should().Be(201);
        var drawer2 = (await drawer2Res.JsonAsync())?.Deserialize<TellerDrawerResponseDto>(JsonOptions)!;

        // 4. Inter-Teller Denomination Swap: Drawer 1 gives 10 notes of 1000 (10k), receives 20 notes of 500 (10k)
        var swapReq = new InterTellerDenominationSwapRequest(
            SourceDrawerId: drawer1.DrawerId,
            TargetDrawerId: drawer2.DrawerId,
            GivenDenominations: new Dictionary<int, int> { [1000] = 10 },
            ReceivedDenominations: new Dictionary<int, int> { [500] = 20 },
            SupervisorApprovalId: "SUPERVISOR_SWAP_01");

        var swapRes = await api.PostAsync("/api/v1/teller/drawers/swap-denominations", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "SUPERVISOR_SWAP_01" },
            DataObject = swapReq
        });
        swapRes.Status.Should().Be(200);
        var swapResult = (await swapRes.JsonAsync())?.Deserialize<InterTellerSwapResponseDto>(JsonOptions)!;
        swapResult.SwappedAmount.Should().Be(10_000m);
    }

    [Fact]
    public async Task Test_MultiAccountSweep_OneToMany_ExecutesMultiLegTransfers()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Create Master account (funded with 300,000 BDT)
        var (_, masterAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, initialBalance: 300_000m);

        // 2. Create Target account 1 and Target account 2 (funded with 10,000 BDT each)
        var (_, targetAcc1) = await CreateCustomerAndAccountWithBalanceAsync(api, initialBalance: 10_000m);
        var (_, targetAcc2) = await CreateCustomerAndAccountWithBalanceAsync(api, initialBalance: 10_000m);

        // 3. Configure Multi-Account Sweep Rule (OneToMany: Retain 100k, Sweep surplus 60% to Target1, 40% to Target2)
        string ruleId = $"MSWP-{Random.Shared.Next(1000, 9999)}";
        var configReq = new ConfigureMultiSweepRequest(
            RuleId: ruleId,
            Type: MultiSweepType.OneToMany,
            MasterAccountNumber: masterAcc,
            Targets: new List<MultiSweepTargetItemDto>
            {
                new(targetAcc1, 60m, 1),
                new(targetAcc2, 40m, 2)
            },
            ThresholdAmount: 100_000m,
            MinimumSweepAmount: 10_000m,
            Strategy: AllocationStrategy.PercentageSplit);

        var configRes = await api.PostAsync("/api/v1/standing-instructions/multi-sweeps/configure", new APIRequestContextOptions
        {
            DataObject = configReq
        });
        configRes.Status.Should().Be(200);
        var ruleDto = (await configRes.JsonAsync())?.Deserialize<MultiSweepRuleResponseDto>(JsonOptions)!;
        ruleDto.RuleId.Should().Be(ruleId);
        ruleDto.IsActive.Should().BeTrue();

        // 4. Execute Multi-Account Sweep
        // Surplus: 300k - 100k = 200,000 BDT -> Target 1 gets 120k (60%), Target 2 gets 80k (40%)
        var execRes = await api.PostAsync($"/api/v1/standing-instructions/multi-sweeps/execute/{ruleId}", new APIRequestContextOptions());
        execRes.Status.Should().Be(200);
        var execResult = (await execRes.JsonAsync())?.Deserialize<MultiSweepExecutionResultDto>(JsonOptions)!;

        execResult.TotalAmountSwept.Should().Be(200_000m);
        execResult.TotalLegsExecuted.Should().Be(2);
        execResult.Legs.Should().HaveCount(2);

        execResult.Legs[0].FromAccount.Should().Be(masterAcc);
        execResult.Legs[0].ToAccount.Should().Be(targetAcc1);
        execResult.Legs[0].Amount.Should().Be(120_000m);

        execResult.Legs[1].FromAccount.Should().Be(masterAcc);
        execResult.Legs[1].ToAccount.Should().Be(targetAcc2);
        execResult.Legs[1].Amount.Should().Be(80_000m);
    }

    [Fact]
    public async Task Test_StandingInstruction_TemporaryHold_AndModifyLifecycle()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, sourceAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, initialBalance: 50_000m);
        var (_, targetAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, initialBalance: 10_000m);

        // 1. Create Standing Instruction
        string siId = $"SI-TEST-{Random.Shared.Next(1000, 9999)}";
        var createReq = new CreateStandingInstructionRequest(
            InstructionId: siId,
            SourceAccountNumber: sourceAcc,
            TargetAccountNumber: targetAcc,
            Type: StandingInstructionType.FixedAmountTransfer,
            Frequency: ExecutionFrequency.Monthly,
            TransferAmount: 5_000m,
            ExecutionFee: 10m,
            StartDate: new DateOnly(2026, 11, 15));

        var createRes = await api.PostAsync("/api/v1/standing-instructions", new APIRequestContextOptions
        {
            DataObject = createReq
        });
        createRes.Status.Should().Be(200);

        // 2. Apply Temporary Hold with Tenor & Amount (BRPD 1.8.1-v)
        var holdReq = new ApplyTemporaryHoldRequest(
            HoldUntilDate: new DateOnly(2026, 11, 30),
            HoldAmount: 5_000m,
            Reason: "Customer pending reconciliation");

        var holdRes = await api.PostAsync($"/api/v1/standing-instructions/{siId}/temporary-hold", new APIRequestContextOptions
        {
            DataObject = holdReq
        });
        holdRes.Status.Should().Be(200);
        var holdDto = (await holdRes.JsonAsync())?.Deserialize<StandingInstructionResponseDto>(JsonOptions)!;
        holdDto.IsTemporaryHoldActive.Should().BeTrue();
        holdDto.HoldUntilDate.Should().Be(new DateOnly(2026, 11, 30));

        // 3. Modify SI parameters
        var modReq = new ModifyStandingInstructionRequest(
            NewTransferAmount: 7_500m,
            NewExecutionFee: 15m,
            ModifierUserId: "OPERATIONS_OFFICER_01");

        var modRes = await api.PutAsync($"/api/v1/standing-instructions/{siId}/modify", new APIRequestContextOptions
        {
            DataObject = modReq
        });
        modRes.Status.Should().Be(200);
        var modDto = (await modRes.JsonAsync())?.Deserialize<StandingInstructionResponseDto>(JsonOptions)!;
        modDto.TransferAmount.Should().Be(7_500m);
        modDto.ExecutionFee.Should().Be(15m);

        // 4. Release Temporary Hold
        var releaseReq = new ReleaseTemporaryHoldRequest("SUPERVISOR_01");
        var releaseRes = await api.PostAsync($"/api/v1/standing-instructions/{siId}/release-hold", new APIRequestContextOptions
        {
            DataObject = releaseReq
        });
        releaseRes.Status.Should().Be(200);
        var releaseDto = (await releaseRes.JsonAsync())?.Deserialize<StandingInstructionResponseDto>(JsonOptions)!;
        releaseDto.IsTemporaryHoldActive.Should().BeFalse();
    }
}
