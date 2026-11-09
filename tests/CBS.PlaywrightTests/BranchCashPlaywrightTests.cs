using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.BranchCash.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class BranchCashPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public BranchCashPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(string Cif, string AccountNumber)> CreateCustomerAndAccountAsync(IAPIRequestContext api, string fullName)
    {
        string uniqueNid = $"8{Random.Shared.Next(100000000, 999999999)}";
        string uniqueTin = $"7{Random.Shared.Next(10000000, 99999999)}999";

        var draftReq = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: fullName,
            DateOfBirth: new DateOnly(1990, 4, 15),
            Gender: "Male",
            FatherName: "Father",
            MotherName: "Mother",
            StreetAddress: "45 Motijheel C/A",
            ThanaOrUpazila: "Motijheel",
            District: "Dhaka",
            PostalCode: "1000",
            MobileNumber: "+8801755667788",
            SectorCode: "9901",
            NationalId: uniqueNid,
            TaxId: uniqueTin);

        var draftRes = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = draftReq
        });
        draftRes.Status.Should().Be(201);
        var draftJson = await draftRes.JsonAsync();
        var customer = draftJson?.Deserialize<CustomerResponseDto>(JsonOptions)!;

        var verifyRes = await api.PostAsync($"/api/v1/customers/{customer.CifNumber}/verify", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "CHECKER01" } },
            DataObject = new { isApproved = true }
        });
        verifyRes.Status.Should().Be(200);

        var openAccReq = new OpenDepositAccountRequest(
            CifNumber: customer.CifNumber,
            ProductType: DepositProductType.SavingsAccount,
            BranchCode: "001",
            InitialDeposit: 0m,
            HasTin: true);

        var accRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = openAccReq
        });
        accRes.Status.Should().Be(201);
        var accJson = await accRes.JsonAsync();
        var account = accJson?.Deserialize<AccountResponseDto>(JsonOptions)!;

        return (customer.CifNumber, account.AccountNumber);
    }

    [Fact]
    public async Task PlaywrightE2E_TellerDrawerOpening_EnforcesDualControlVaultHandoff()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Vault handoff where Cashier and Supervisor are identical must fail
        var invalidOpenReq = new OpenTellerDrawerRequest(
            BranchCode: "001",
            CashierUserId: "CASHIER_SAME",
            InitialVaultAllocation: 100000m,
            Denominations: new Dictionary<int, int> { { 1000, 100 } });

        var failRes = await api.PostAsync("/api/v1/teller/drawers/open", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Supervisor-Id", "CASHIER_SAME" } },
            DataObject = invalidOpenReq
        });
        failRes.Status.Should().Be(400);
        var failBody = await failRes.TextAsync();
        failBody.Should().Contain("MakerCheckerSeparationInvariant");

        // 2. Dual-control vault handoff where Cashier != Supervisor must succeed
        var validOpenReq = new OpenTellerDrawerRequest(
            BranchCode: "001",
            CashierUserId: "CASHIER_01",
            InitialVaultAllocation: 150000m,
            Denominations: new Dictionary<int, int> { { 1000, 100 }, { 500, 100 } });

        var successRes = await api.PostAsync("/api/v1/teller/drawers/open", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Supervisor-Id", "SUPERVISOR_VAULT" } },
            DataObject = validOpenReq
        });
        successRes.Status.Should().Be(201);
        var successJson = await successRes.JsonAsync();
        var drawer = successJson?.Deserialize<TellerDrawerResponseDto>(JsonOptions)!;

        drawer.Status.Should().Be(TellerDrawerStatus.Active);
        drawer.CurrentBalance.Should().Be(150000m);
        drawer.CashierUserId.Should().Be("CASHIER_01");
        drawer.OpeningSupervisorUserId.Should().Be("SUPERVISOR_VAULT");
    }

    [Fact]
    public async Task PlaywrightE2E_CashDeposit_EnforcesCashierLimits_AndBfiuCtrThreshold()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNum) = await CreateCustomerAndAccountAsync(api, "Fazle Hasan Abed");

        // Open teller drawer with 500k limit
        var openDrawerReq = new OpenTellerDrawerRequest(
            BranchCode: "001",
            CashierUserId: "CASHIER_LIMIT_TEST",
            InitialVaultAllocation: 50000m,
            Denominations: new Dictionary<int, int> { { 1000, 50 } },
            PostingLimit: 500000m);

        var drawerRes = await api.PostAsync("/api/v1/teller/drawers/open", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Supervisor-Id", "SUP_LIMIT" } },
            DataObject = openDrawerReq
        });
        drawerRes.Status.Should().Be(201);
        var drawer = (await drawerRes.JsonAsync())?.Deserialize<TellerDrawerResponseDto>(JsonOptions)!;

        // 1. Deposit 40,000 BDT within cashier posting limit
        var depositReq = new ProcessCashTransactionRequest(
            DrawerId: drawer.DrawerId,
            AccountNumber: accNum,
            MovementType: CashMovementType.CustomerCashDeposit,
            Amount: 40000m,
            Denominations: new Dictionary<int, int> { { 1000, 40 } });

        var depRes = await api.PostAsync("/api/v1/teller/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Cashier-Id", "CASHIER_LIMIT_TEST" } },
            DataObject = depositReq
        });
        depRes.Status.Should().Be(200);
        var depResult = (await depRes.JsonAsync())?.Deserialize<CashTransactionResponseDto>(JsonOptions)!;
        depResult.AccountBalance.Should().Be(40000m);
        depResult.DrawerBalance.Should().Be(90000m); // 50k initial + 40k deposit
        depResult.BfiuCtrReportable.Should().BeFalse();

        // 2. Deposit 600,000 BDT without supervisor authorization must fail (exceeds 500k cashier limit)
        var limitBreachReq = new ProcessCashTransactionRequest(
            DrawerId: drawer.DrawerId,
            AccountNumber: accNum,
            MovementType: CashMovementType.CustomerCashDeposit,
            Amount: 600000m,
            Denominations: new Dictionary<int, int> { { 1000, 600 } },
            SupervisorApprovalId: null);

        var breachRes = await api.PostAsync("/api/v1/teller/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Cashier-Id", "CASHIER_LIMIT_TEST" } },
            DataObject = limitBreachReq
        });
        breachRes.Status.Should().Be(400);
        var breachBody = await breachRes.TextAsync();
        breachBody.Should().Contain("CashierPostingLimitExceededInvariant");

        // 3. Deposit 1,200,000 BDT with supervisor authorization flags BFIU CTR threshold
        var ctrDepositReq = new ProcessCashTransactionRequest(
            DrawerId: drawer.DrawerId,
            AccountNumber: accNum,
            MovementType: CashMovementType.CustomerCashDeposit,
            Amount: 1200000m,
            Denominations: new Dictionary<int, int> { { 1000, 1200 } },
            SupervisorApprovalId: "SUP_AUTH_OVERRIDE_001");

        var ctrRes = await api.PostAsync("/api/v1/teller/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Cashier-Id", "CASHIER_LIMIT_TEST" } },
            DataObject = ctrDepositReq
        });
        ctrRes.Status.Should().Be(200);
        var ctrResult = (await ctrRes.JsonAsync())?.Deserialize<CashTransactionResponseDto>(JsonOptions)!;
        ctrResult.BfiuCtrReportable.Should().BeTrue();
        ctrResult.Message.Should().Contain("BFIU CTR ALERT");
        ctrResult.AccountBalance.Should().Be(1240000m); // 40k + 1.2M
    }

    [Fact]
    public async Task PlaywrightE2E_RemittancePayOrder_FullLifecycle_Issue_Liquidate_Stop_Cancel()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Seed security stationery book under joint custody
        var seedReq = new SeedStationeryRequest(
            BranchCode: "001",
            Type: RemittanceInstrumentType.PayOrder,
            Prefix: "PO",
            StartLeaf: 1001,
            EndLeaf: 1050,
            ManagerUserId: "MANAGER_SEED",
            CustodianUserId: "CUSTODIAN_SEED");

        var seedRes = await api.PostAsync("/api/v1/remittance/stationery/seed", new APIRequestContextOptions
        {
            DataObject = seedReq
        });
        seedRes.Status.Should().Be(200);

        // 2. Issue Pay Order: 100,000 BDT
        // Tiered Commission: 50.00 BDT
        // 15% NBR VAT: 7.50 BDT
        // Total Payable: 100,057.50 BDT
        var issueReq = new IssueRemittanceInstrumentRequest(
            BranchCode: "001",
            Type: RemittanceInstrumentType.PayOrder,
            Amount: 100000m,
            BeneficiaryName: "Dhaka Power Distribution Company (DPDC)",
            PurchaserCif: "CIF88776655");

        var issueRes = await api.PostAsync("/api/v1/remittance/issue", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>
            {
                { "X-Maker-Id", "MAKER_OFFICER" },
                { "X-Checker-Id", "CHECKER_OFFICER" }
            },
            DataObject = issueReq
        });
        issueRes.Status.Should().Be(201);
        var instrument = (await issueRes.JsonAsync())?.Deserialize<RemittanceInstrumentResponseDto>(JsonOptions)!;

        instrument.PrincipalAmount.Should().Be(100000m);
        instrument.Commission.Should().Be(50m);
        instrument.VatAmount.Should().Be(7.50m);
        instrument.TotalPayable.Should().Be(100057.50m);
        instrument.Status.Should().Be(RemittanceInstrumentStatus.Issued);

        // 3. Liquidate Instrument
        var liquidateReq = new LiquidateInstrumentRequest(
            InstrumentNumber: instrument.InstrumentNumber,
            PresentingBranchCode: "002");

        var liqRes = await api.PostAsync("/api/v1/remittance/liquidate", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Teller-Id", "TELLER_CLEARING" } },
            DataObject = liquidateReq
        });
        liqRes.Status.Should().Be(200);
        var liqResult = (await liqRes.JsonAsync())?.Deserialize<RemittanceInstrumentResponseDto>(JsonOptions)!;
        liqResult.Status.Should().Be(RemittanceInstrumentStatus.Liquidated);
        liqResult.LiquidatedBranchCode.Should().Be("002");

        // 4. Duplicate liquidation attempt must be blocked
        var dupLiqRes = await api.PostAsync("/api/v1/remittance/liquidate", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Teller-Id", "TELLER_CLEARING" } },
            DataObject = liquidateReq
        });
        dupLiqRes.Status.Should().Be(400);
        var dupLiqBody = await dupLiqRes.TextAsync();
        dupLiqBody.Should().Contain("DuplicateLiquidationBlockedInvariant");

        // 5. Issue another PO and test Stop Payment workflow
        var issue2Req = new IssueRemittanceInstrumentRequest(
            BranchCode: "001",
            Type: RemittanceInstrumentType.PayOrder,
            Amount: 20000m,
            BeneficiaryName: "Titas Gas",
            PurchaserCif: "CIF88776655");

        var issue2Res = await api.PostAsync("/api/v1/remittance/issue", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>
            {
                { "X-Maker-Id", "MAKER_OFFICER" },
                { "X-Checker-Id", "CHECKER_OFFICER" }
            },
            DataObject = issue2Req
        });
        issue2Res.Status.Should().Be(201);
        var inst2 = (await issue2Res.JsonAsync())?.Deserialize<RemittanceInstrumentResponseDto>(JsonOptions)!;

        // Stop payment
        var stopReq = new StopPaymentRequest(inst2.InstrumentNumber, "Purchaser lost cheque");
        var stopRes = await api.PostAsync("/api/v1/remittance/stop-payment", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Supervisor-Id", "SUPERVISOR_STOP" } },
            DataObject = stopReq
        });
        stopRes.Status.Should().Be(200);
        var stopped = (await stopRes.JsonAsync())?.Deserialize<RemittanceInstrumentResponseDto>(JsonOptions)!;
        stopped.Status.Should().Be(RemittanceInstrumentStatus.Stopped);

        // Attempting to liquidate stopped instrument must fail
        var stopLiqReq = new LiquidateInstrumentRequest(inst2.InstrumentNumber, "001");
        var stopLiqRes = await api.PostAsync("/api/v1/remittance/liquidate", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Teller-Id", "TELLER_CLEARING" } },
            DataObject = stopLiqReq
        });
        stopLiqRes.Status.Should().Be(400);
        var stopLiqBody = await stopLiqRes.TextAsync();
        stopLiqBody.Should().Contain("StoppedInstrumentLiquidationBlockedInvariant");
    }
}
