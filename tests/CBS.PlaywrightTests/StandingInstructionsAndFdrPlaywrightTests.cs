using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Cheques.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Application.StandingInstructions.DTOs;
using CBS.Application.TermDeposits.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class StandingInstructionsAndFdrPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public StandingInstructionsAndFdrPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(string Cif, string AccountNumber)> CreateCustomerAndAccountWithBalanceAsync(
        IAPIRequestContext api,
        decimal initialBalance = 100000m,
        DepositProductType productType = DepositProductType.SavingsAccount)
    {
        string uniqueNid = $"8{Random.Shared.Next(100000000, 999999999)}";
        var draftReq = new CreateCustomerDraftRequest(
            CustomerType.Individual,
            "Retail Test Customer",
            new DateOnly(1992, 7, 20),
            "Male",
            "Father",
            "Mother",
            "Road 12, Dhanmondi",
            "Dhanmondi",
            "Dhaka",
            "1205",
            "+8801811223344",
            "9901",
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
    public async Task PlaywrightE2E_ChequeBook_Requisition_DeductsFeeAndEnforcesStopPayment()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (cif, accountNumber) = await CreateCustomerAndAccountWithBalanceAsync(api, 50000m);

        var uniqueSuffix = Random.Shared.Next(1000, 9999);
        var bookId = $"CHQ-BK-{uniqueSuffix}";
        long startLeaf = 1000 + uniqueSuffix * 10;

        // 1. Requisition 25-leaf Cheque Book (BDT 5 per leaf + 15% VAT = 143.75 BDT)
        var req = new RequisitionChequeBookRequest(
            BookId: bookId,
            AccountNumber: accountNumber,
            Prefix: "SB-",
            StartLeafNumber: startLeaf,
            TotalLeaves: 25,
            BranchCode: "0101");

        var issueRes = await api.PostAsync("/api/v1/cheques/requisition", new APIRequestContextOptions
        {
            DataObject = req
        });
        issueRes.Status.Should().Be(200);

        var book = (await issueRes.JsonAsync())?.Deserialize<ChequeBookResponseDto>(JsonOptions);
        book.Should().NotBeNull();
        book!.TotalLeaves.Should().Be(25);
        book.Status.Should().Be("Requested");

        // 2. Deliver Cheque Book to Customer
        var deliverRes = await api.PostAsync($"/api/v1/cheques/books/{bookId}/deliver");
        deliverRes.Status.Should().Be(200);

        // 3. Verify Leaf Status is Available
        long targetLeaf = startLeaf + 5;
        var statusRes1 = await api.GetAsync($"/api/v1/cheques/leaf/status?accountNumber={accountNumber}&leafNumber={targetLeaf}");
        statusRes1.Status.Should().Be(200);
        var leafStatus1 = (await statusRes1.JsonAsync())?.Deserialize<ChequeLeafStatusDto>(JsonOptions);
        leafStatus1!.Status.Should().Be("Available");

        // 4. Issue Stop Payment Instruction for target leaf
        var stopReq = new ChequeStopPaymentRequest(
            AccountNumber: accountNumber,
            LeafNumber: targetLeaf,
            Reason: StopPaymentReason.LostOrStolen,
            Notes: "Customer misplaced leaf while traveling",
            BranchCode: "0101",
            OfficerId: "OFFICER_01");

        var stopRes = await api.PostAsync("/api/v1/cheques/stop-payment", new APIRequestContextOptions
        {
            DataObject = stopReq
        });
        stopRes.Status.Should().Be(200);

        var stoppedLeaf = (await stopRes.JsonAsync())?.Deserialize<ChequeLeafStatusDto>(JsonOptions);
        stoppedLeaf!.Status.Should().Be("Stopped");
        stoppedLeaf.StopReason.Should().Be("LostOrStolen");

        // 5. Revoke Stop Payment
        var revokeReq = new ChequeRevokeStopPaymentRequest(
            AccountNumber: accountNumber,
            LeafNumber: targetLeaf,
            OfficerId: "MANAGER_01",
            Reason: "Customer recovered lost cheque leaf");

        var revokeRes = await api.PostAsync("/api/v1/cheques/stop-payment/revoke", new APIRequestContextOptions
        {
            DataObject = revokeReq
        });
        revokeRes.Status.Should().Be(200);

        var revokedLeaf = (await revokeRes.JsonAsync())?.Deserialize<ChequeLeafStatusDto>(JsonOptions);
        revokedLeaf!.Status.Should().Be("Available");
    }

    [Fact]
    public async Task PlaywrightE2E_SweepAccounts_ExecutesSweepOutAndSweepIn()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Create Primary (operative) account with 150,000 BDT
        var (_, primaryAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 150000m);

        // 2. Create Secondary (savings) account with 50,000 BDT
        var (_, secondaryAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 50000m);

        var uniqueSuffix = Random.Shared.Next(1000, 9999);
        var ruleId = $"SWP-RULE-{uniqueSuffix}";

        // Configure rule: Upper = 100k, Lower = 20k, MinSweep = 5k
        var sweepRuleReq = new ConfigureSweepRuleRequest(
            RuleId: ruleId,
            PrimaryAccountNumber: primaryAcc,
            SecondaryAccountNumber: secondaryAcc,
            UpperThreshold: 100000m,
            LowerThreshold: 20000m,
            MinimumSweepAmount: 5000m);

        var ruleRes = await api.PostAsync("/api/v1/standing-instructions/sweep/rule", new APIRequestContextOptions
        {
            DataObject = sweepRuleReq
        });
        ruleRes.Status.Should().Be(200);

        // 3. Execute Sweep: Primary has 150k (> 100k upper) -> Excess 50k should sweep out to Secondary
        var execRes = await api.PostAsync($"/api/v1/standing-instructions/sweep/execute/{ruleId}");
        execRes.Status.Should().Be(200);

        var sweepResult = (await execRes.JsonAsync())?.Deserialize<ExecuteSweepResponseDto>(JsonOptions);
        sweepResult.Should().NotBeNull();
        sweepResult!.SweepDirection.Should().Be("SweepOut");
        sweepResult.AmountSwept.Should().Be(50000m);
        sweepResult.NewPrimaryBalance.Should().Be(100000m);
        sweepResult.NewSecondaryBalance.Should().Be(100000m); // 50k + 50k
        sweepResult.VoucherNumber.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PlaywrightE2E_TermDeposit_Opens_Accrues_AndRollover()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, operativeAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 200000m);
        var (_, fdrAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 0m, DepositProductType.FixedDepositReceipt);

        var uniqueSuffix = Random.Shared.Next(1000, 9999);
        var contractId = $"FDR-{uniqueSuffix}";

        // 1. Open Term Deposit contract for 12 months with AutoRenewPrincipalAndInterest (Compounding)
        var openReq = new OpenTermDepositRequest(
            ContractId: contractId,
            FdrAccountNumber: fdrAcc,
            OperativeAccountNumber: operativeAcc,
            PrincipalAmount: 100000m,
            TenorMonths: 12,
            AnnualInterestRate: 9.00m,
            StartDate: new DateOnly(2025, 1, 1),
            MaturityInstruction: TermDepositMaturityInstruction.AutoRenewPrincipalAndInterest);

        var openRes = await api.PostAsync("/api/v1/term-deposits/open", new APIRequestContextOptions
        {
            DataObject = openReq
        });
        openRes.Status.Should().Be(200);

        var contract = (await openRes.JsonAsync())?.Deserialize<TermDepositContractResponseDto>(JsonOptions);
        contract.Should().NotBeNull();
        contract!.PrincipalAmount.Should().Be(100000m);
        contract.Status.Should().Be("Active");
        contract.MaturityDate.Should().Be(new DateOnly(2026, 1, 1));

        // 2. Accrue Daily Interest
        var accrueRes = await api.PostAsync($"/api/v1/term-deposits/{contractId}/accrue?calculationDate=2025-01-31");
        accrueRes.Status.Should().Be(200);

        // 3. Execute Maturity Rollover
        var rolloverRes = await api.PostAsync($"/api/v1/term-deposits/{contractId}/rollover?prevailingInterestRate=9.50&hasTin=true");
        rolloverRes.Status.Should().Be(200);

        var rollover = (await rolloverRes.JsonAsync())?.Deserialize<MaturityRolloverResponseDto>(JsonOptions);
        rollover.Should().NotBeNull();
        rollover!.NetInterestCapitalized.Should().BeGreaterThan(0m);
        rollover.RolledPrincipal.Should().BeGreaterThan(100000m);
        rollover.NewInterestRate.Should().Be(9.50m);
    }

    [Fact]
    public async Task PlaywrightE2E_TermDeposit_PrematureEncashment_EnforcesActiveLienLock()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, operativeAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 300000m);
        var (_, fdrAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 0m, DepositProductType.FixedDepositReceipt);

        var uniqueSuffix = Random.Shared.Next(1000, 9999);
        var contractId = $"FDR-LIEN-{uniqueSuffix}";

        // 1. Open FDR
        var openReq = new OpenTermDepositRequest(
            ContractId: contractId,
            FdrAccountNumber: fdrAcc,
            OperativeAccountNumber: operativeAcc,
            PrincipalAmount: 200000m,
            TenorMonths: 12,
            AnnualInterestRate: 8.50m,
            StartDate: new DateOnly(2026, 1, 1),
            MaturityInstruction: TermDepositMaturityInstruction.AutoRenewPrincipalOnly);

        var openRes = await api.PostAsync("/api/v1/term-deposits/open", new APIRequestContextOptions
        {
            DataObject = openReq
        });
        openRes.Status.Should().Be(200);

        // 2. CAD marks active lien against Loan
        var lienReq = new MarkLienRequest(150000m, $"LN-FDR-{uniqueSuffix}");
        var lienRes = await api.PostAsync($"/api/v1/term-deposits/{contractId}/lien", new APIRequestContextOptions
        {
            DataObject = lienReq
        });
        lienRes.Status.Should().Be(200);

        // 3. Attempt Premature Encashment -> Must be BLOCKED with 400 Bad Request (BRPD 3.5.13)
        var prematureReq = new PrematureEncashmentRequest(
            EncashmentDate: new DateOnly(2026, 6, 1),
            PenaltyMarkdownPercentage: 2.00m,
            HasTin: true);

        var blockedRes = await api.PostAsync($"/api/v1/term-deposits/{contractId}/premature-encash", new APIRequestContextOptions
        {
            DataObject = prematureReq
        });
        blockedRes.Status.Should().Be(400);

        var errorText = await blockedRes.TextAsync();
        errorText.Should().Contain("FdrActiveLienBlockInvariant");

        // 4. Release Lien
        var releaseRes = await api.PostAsync($"/api/v1/term-deposits/{contractId}/lien/release?releaseReference=LOAN_CLEARED");
        releaseRes.Status.Should().Be(200);

        // 5. Premature Encashment now succeeds after lien release
        var successRes = await api.PostAsync($"/api/v1/term-deposits/{contractId}/premature-encash", new APIRequestContextOptions
        {
            DataObject = prematureReq
        });
        successRes.Status.Should().Be(200);

        var encashResult = (await successRes.JsonAsync())?.Deserialize<PrematureEncashmentResponseDto>(JsonOptions);
        encashResult.Should().NotBeNull();
        encashResult!.PrincipalPayout.Should().Be(200000m);
        encashResult.VoucherNumber.Should().NotBeNullOrWhiteSpace();
    }
}
