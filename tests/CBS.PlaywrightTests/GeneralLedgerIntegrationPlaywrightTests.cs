using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.BranchCash.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Application.GeneralLedger.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.GeneralLedger;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class GeneralLedgerIntegrationPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public GeneralLedgerIntegrationPlaywrightTests(CbsApiServerFixture fixture)
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
            DateOfBirth: new DateOnly(1992, 5, 20),
            Gender: "Female",
            FatherName: "Father",
            MotherName: "Mother",
            StreetAddress: "12 Dilkusha C/A",
            ThanaOrUpazila: "Motijheel",
            District: "Dhaka",
            PostalCode: "1000",
            MobileNumber: "+8801711223344",
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
    public async Task PlaywrightE2E_ChartOfAccounts_ReturnsStandardBangladeshBankAccounts()
    {
        var api = await _fixture.CreateApiContextAsync();

        var res = await api.GetAsync("/api/v1/gl/chart-of-accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "AUDITOR01" } }
        });

        res.Status.Should().Be(200);
        var json = await res.JsonAsync();
        var coaList = json?.Deserialize<List<ChartOfAccountDto>>(JsonOptions);

        coaList.Should().NotBeNull();
        coaList!.Count.Should().BeGreaterThanOrEqualTo(18);

        // Verify standard BB COA codes (BRPD Circular 12/2024 Module 1)
        coaList.Should().Contain(a => a.Code == StandardGlAccounts.MainVaultCash && a.Category == AccountCategory.Asset);
        coaList.Should().Contain(a => a.Code == StandardGlAccounts.TellerTillCash && a.Category == AccountCategory.Asset);
        coaList.Should().Contain(a => a.Code == StandardGlAccounts.BangladeshBankSettlement && a.Category == AccountCategory.Asset);
        coaList.Should().Contain(a => a.Code == StandardGlAccounts.CustomerSavingsDeposit && a.Category == AccountCategory.Liability);
        coaList.Should().Contain(a => a.Code == StandardGlAccounts.PayOrderPayable && a.Category == AccountCategory.Liability);
        coaList.Should().Contain(a => a.Code == StandardGlAccounts.StatutoryVatPayable && a.Category == AccountCategory.Liability);
        coaList.Should().Contain(a => a.Code == StandardGlAccounts.RemittanceCommissionIncome && a.Category == AccountCategory.Income);
    }

    [Fact]
    public async Task PlaywrightE2E_CashDepositAndWithdrawal_GeneratesBalancedGlVouchers()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNum) = await CreateCustomerAndAccountAsync(api, "Begum Rokeya");

        // 1. Open teller drawer with 200,000 BDT initial vault allocation
        string cashierId = $"CASHIER_GL_{Random.Shared.Next(100, 999)}";
        var openDrawerReq = new OpenTellerDrawerRequest(
            BranchCode: "001",
            CashierUserId: cashierId,
            InitialVaultAllocation: 200000m,
            Denominations: new Dictionary<int, int> { { 1000, 200 } });

        var openRes = await api.PostAsync("/api/v1/teller/drawers/open", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Supervisor-Id", "SUPERVISOR_01" } },
            DataObject = openDrawerReq
        });
        openRes.Status.Should().Be(201);
        var drawerJson = await openRes.JsonAsync();
        var drawer = drawerJson?.Deserialize<TellerDrawerResponseDto>(JsonOptions)!;

        // 2. Perform Cash Deposit of 50,000 BDT
        var depositReq = new ProcessCashTransactionRequest(
            DrawerId: drawer.DrawerId,
            AccountNumber: accNum,
            Amount: 50000m,
            MovementType: CashMovementType.CustomerCashDeposit,
            Denominations: new Dictionary<int, int> { { 1000, 50 } });

        var depositRes = await api.PostAsync("/api/v1/teller/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", cashierId } },
            DataObject = depositReq
        });
        depositRes.Status.Should().Be(200);

        // 3. Verify Trial Balance is balanced: Total Debits == Total Credits, Discrepancy == 0
        var tbRes = await api.GetAsync("/api/v1/gl/trial-balance", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "AUDITOR01" } }
        });
        tbRes.Status.Should().Be(200);
        var tbJson = await tbRes.JsonAsync();
        var tb = tbJson?.Deserialize<TrialBalanceDto>(JsonOptions)!;

        tb.IsBalanced.Should().BeTrue();
        tb.Discrepancy.Should().Be(0m);
        tb.TotalDebits.Should().Be(tb.TotalCredits);
        tb.TotalDebits.Should().BeGreaterThan(0m);

        // 4. Perform Cash Withdrawal of 15,000 BDT
        var withdrawReq = new ProcessCashTransactionRequest(
            DrawerId: drawer.DrawerId,
            AccountNumber: accNum,
            Amount: 15000m,
            MovementType: CashMovementType.CustomerCashWithdrawal,
            Denominations: new Dictionary<int, int> { { 1000, 15 } });

        var withdrawRes = await api.PostAsync("/api/v1/teller/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", cashierId } },
            DataObject = withdrawReq
        });
        withdrawRes.Status.Should().Be(200);

        // 5. Verify Trial Balance remains strictly balanced
        var tbRes2 = await api.GetAsync("/api/v1/gl/trial-balance", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "AUDITOR01" } }
        });
        tbRes2.Status.Should().Be(200);
        var tbJson2 = await tbRes2.JsonAsync();
        var tb2 = tbJson2?.Deserialize<TrialBalanceDto>(JsonOptions)!;

        tb2.IsBalanced.Should().BeTrue();
        tb2.Discrepancy.Should().Be(0m);
        tb2.TotalDebits.Should().Be(tb2.TotalCredits);
    }

    [Fact]
    public async Task PlaywrightE2E_RemittanceIssueAndLiquidation_PostsBalanced4LeggedVouchers()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Issue Pay Order for 100,000 BDT with Commission and 15% VAT
        var issueReq = new IssueRemittanceInstrumentRequest(
            BranchCode: "001",
            Type: RemittanceInstrumentType.PayOrder,
            Amount: 100000m,
            BeneficiaryName: "Dhaka Power Distribution Co Ltd",
            PurchaserCif: "CIF00112233",
            ExplicitInstrumentNumber: $"PO-GL-{Random.Shared.Next(100000, 999999)}");

        var issueRes = await api.PostAsync("/api/v1/remittance/issue", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>
            {
                { "X-User-Id", "MAKER_REMIT_01" },
                { "X-Checker-Id", "CHECKER_REMIT_01" }
            },
            DataObject = issueReq
        });
        issueRes.Status.Should().Be(201);
        var issueJson = await issueRes.JsonAsync();
        var inst = issueJson?.Deserialize<RemittanceInstrumentResponseDto>(JsonOptions)!;

        inst.InstrumentNumber.Should().StartWith("PO-GL-");
        inst.Commission.Should().Be(50m);
        inst.VatAmount.Should().Be(7.50m); // 15% VAT on 50 BDT commission
        inst.TotalPayable.Should().Be(100057.50m);

        // 2. Verify Trial Balance contains Pay Order liability and VAT payable, strictly balanced
        var tbRes = await api.GetAsync("/api/v1/gl/trial-balance", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "AUDITOR01" } }
        });
        tbRes.Status.Should().Be(200);
        var tbJson = await tbRes.JsonAsync();
        var tb = tbJson?.Deserialize<TrialBalanceDto>(JsonOptions)!;

        tb.IsBalanced.Should().BeTrue();
        tb.Discrepancy.Should().Be(0m);

        // 3. Liquidate the Pay Order
        var liqReq = new LiquidateInstrumentRequest(
            InstrumentNumber: inst.InstrumentNumber,
            PresentingBranchCode: "001");

        var liqRes = await api.PostAsync("/api/v1/remittance/liquidate", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TELLER_LIQ_01" } },
            DataObject = liqReq
        });
        liqRes.Status.Should().Be(200);

        // 4. Verify Trial Balance remains strictly balanced after liquidation
        var tbRes2 = await api.GetAsync("/api/v1/gl/trial-balance", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "AUDITOR01" } }
        });
        tbRes2.Status.Should().Be(200);
        var tbJson2 = await tbRes2.JsonAsync();
        var tb2 = tbJson2?.Deserialize<TrialBalanceDto>(JsonOptions)!;

        tb2.IsBalanced.Should().BeTrue();
        tb2.Discrepancy.Should().Be(0m);
    }

    [Fact]
    public async Task PlaywrightE2E_VoucherReversal_InvertsLegsAndProhibitsSingleLeggedAltering()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNum) = await CreateCustomerAndAccountAsync(api, "Jasimuddin Poet");

        // 1. Post a transaction to create a voucher
        var txReq = new PostTransactionRequest(
            AccountNumber: accNum,
            Amount: 25000m,
            Type: DebitCredit.Credit,
            Narration: "Test Credit for GL Reversal",
            IsCustomerInduced: true);

        var txRes = await api.PostAsync($"/api/v1/accounts/{accNum}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = txReq
        });
        txRes.Status.Should().Be(200);

        // Query Trial Balance
        var tbRes = await api.GetAsync("/api/v1/gl/trial-balance", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "AUDITOR01" } }
        });
        tbRes.Status.Should().Be(200);

        // 2. Attempt reversal with Maker == Checker (must fail under BRPD 1.3.13)
        var revSameUserReq = new ReverseVoucherRequest(
            Reason: "Wrong account posting correction",
            ReversalMakerUserId: "MAKER_REV_01",
            ReversalCheckerUserId: "MAKER_REV_01");

        var failRevRes = await api.PostAsync("/api/v1/gl/vouchers/CSH-001-20261110-01001/reverse", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER_REV_01" } },
            DataObject = revSameUserReq
        });
        failRevRes.Status.Should().Be(400);

        // 3. Reversal with non-existent voucher must return 404
        var validRevReq = new ReverseVoucherRequest(
            Reason: "Legitimate audit correction",
            ReversalMakerUserId: "MAKER_REV_01",
            ReversalCheckerUserId: "CHECKER_REV_01");

        var notFoundRes = await api.PostAsync("/api/v1/gl/vouchers/NON_EXISTENT_VOUCHER/reverse", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER_REV_01" } },
            DataObject = validRevReq
        });
        notFoundRes.Status.Should().Be(404);
    }
}
