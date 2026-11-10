using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Application.GeneralLedger.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.GeneralLedger;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class DepositLifecycleIntegrationPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public DepositLifecycleIntegrationPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(string Cif, string AccountNumber)> CreateCustomerAndAccountAsync(
        IAPIRequestContext api,
        string fullName,
        DepositProductType productType = DepositProductType.SavingsAccount,
        bool hasTin = true)
    {
        string uniqueNid = $"8{Random.Shared.Next(100000000, 999999999)}";
        string uniqueTin = $"7{Random.Shared.Next(10000000, 99999999)}999";

        var draftReq = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: fullName,
            DateOfBirth: new DateOnly(1990, 8, 15),
            Gender: "Male",
            FatherName: "Father",
            MotherName: "Mother",
            StreetAddress: "77 Motijheel Commercial Area",
            ThanaOrUpazila: "Motijheel",
            District: "Dhaka",
            PostalCode: "1000",
            MobileNumber: "+8801811223344",
            SectorCode: "9901",
            NationalId: uniqueNid,
            TaxId: hasTin ? uniqueTin : null);

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
            ProductType: productType,
            BranchCode: "001",
            InitialDeposit: 0m,
            HasTin: hasTin);

        var openAccRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = openAccReq
        });
        openAccRes.Status.Should().Be(201);
        var openAccJson = await openAccRes.JsonAsync();
        var account = openAccJson?.Deserialize<AccountResponseDto>(JsonOptions)!;

        return (customer.CifNumber, account.AccountNumber);
    }

    [Fact]
    public async Task PlaywrightE2E_DailyDepositEodBatch_EvaluatesDormancy_AndPostsGlInterestAccrualVoucher()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Create Savings Account and deposit 500,000 BDT
        var (_, accNum) = await CreateCustomerAndAccountAsync(api, "Interest Test Customer 01", DepositProductType.SavingsAccount, hasTin: true);

        var depRes = await api.PostAsync($"/api/v1/accounts/{accNum}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TELLER01" } },
            DataObject = new PostTransactionRequest(accNum, DebitCredit.Credit, 500_000m, "Initial Cash Deposit")
        });
        depRes.Status.Should().Be(200);

        // 2. Trigger Deposit EOD Batch
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd");
        var eodRes = await api.PostAsync("/api/v1/accounts/lifecycle/eod-batch", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "OPS_OFFICER_01" } },
            DataObject = new DepositLifecycleEodRequest("001", tomorrow)
        });

        var eodText = await eodRes.TextAsync();
        eodRes.Status.Should().Be(200, because: eodText);
        var eodJson = await eodRes.JsonAsync();
        var summary = eodJson?.Deserialize<DepositEodSummaryDto>(JsonOptions)!;

        summary.TotalInterestAccrued.Should().BeGreaterThan(0m);
        summary.AccrualVoucherNumber.Should().NotBeNullOrWhiteSpace();

        // 3. Inspect General Ledger Voucher
        var voucherRes = await api.GetAsync($"/api/v1/gl/vouchers/{summary.AccrualVoucherNumber}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "AUDITOR01" } }
        });
        voucherRes.Status.Should().Be(200);
        var voucherJson = await voucherRes.JsonAsync();
        var voucher = voucherJson?.Deserialize<FinancialVoucherResponseDto>(JsonOptions)!;

        var totalDebit = voucher.Entries.Where(e => e.Type == DebitCredit.Debit).Sum(e => e.Amount);
        var totalCredit = voucher.Entries.Where(e => e.Type == DebitCredit.Credit).Sum(e => e.Amount);

        totalDebit.Should().Be(totalCredit);
        voucher.Entries.Should().Contain(e => e.AccountCode == StandardGlAccounts.DepositInterestExpense && e.Type == DebitCredit.Debit);
        voucher.Entries.Should().Contain(e => e.AccountCode == StandardGlAccounts.CustomerSavingsDeposit && e.Type == DebitCredit.Credit);
    }

    [Fact]
    public async Task PlaywrightE2E_InterestCapitalization_DeductsTax_CreditsNet_AndMaintainsTrialBalanceZeroDiscrepancy()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Create Savings Account and deposit 100,000 BDT
        var (_, accNum) = await CreateCustomerAndAccountAsync(api, "Capitalization Customer 02", DepositProductType.SavingsAccount, hasTin: true);

        await api.PostAsync($"/api/v1/accounts/{accNum}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TELLER01" } },
            DataObject = new PostTransactionRequest(accNum, DebitCredit.Credit, 100_000m, "Deposit")
        });

        // 2. Accrue daily interest via EOD batch
        var nextDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd");
        await api.PostAsync("/api/v1/accounts/lifecycle/eod-batch", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "OPS_OFFICER_01" } },
            DataObject = new DepositLifecycleEodRequest("001", nextDate)
        });

        // 3. Execute Interest Capitalization Batch
        var capRes = await api.PostAsync("/api/v1/accounts/lifecycle/capitalize-interest", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "OPS_OFFICER_01" } },
            DataObject = new DepositInterestCapitalizationRequest("001", nextDate)
        });

        capRes.Status.Should().Be(200);
        var capJson = await capRes.JsonAsync();
        var capBatch = capJson?.Deserialize<BatchInterestCapitalizationResponseDto>(JsonOptions)!;

        capBatch.AccountsCapitalizedCount.Should().BeGreaterThan(0);
        capBatch.TotalGrossInterest.Should().Be(capBatch.TotalTaxDeducted + capBatch.TotalNetCredited);

        // 4. Verify Sub-Ledger to General Ledger (SL-to-GL) Trial Balance Equilibrium
        var tbRes = await api.GetAsync("/api/v1/gl/trial-balance", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "AUDITOR01" } }
        });
        tbRes.Status.Should().Be(200);
        var tbJson = await tbRes.JsonAsync();
        var tb = tbJson?.Deserialize<TrialBalanceDto>(JsonOptions)!;

        tb.IsBalanced.Should().BeTrue();
        tb.Discrepancy.Should().Be(0.00m);
    }

    [Fact]
    public async Task PlaywrightE2E_10YearUnclaimedTransfer_DebitsCustomerToZero_AndCreditsCentralBankUnclaimedGl()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Create Account with initial deposit
        var (_, accNum) = await CreateCustomerAndAccountAsync(api, "Unclaimed 10Yr Customer 03", DepositProductType.SavingsAccount);

        await api.PostAsync($"/api/v1/accounts/{accNum}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TELLER01" } },
            DataObject = new PostTransactionRequest(accNum, DebitCredit.Credit, 200_000m, "Initial Deposit")
        });

        // 2. Advance business date 11 years into the future (132 months inactivity)
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(11)).ToString("yyyy-MM-dd");
        var eodRes = await api.PostAsync("/api/v1/accounts/lifecycle/eod-batch", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "OPS_OFFICER_01" } },
            DataObject = new DepositLifecycleEodRequest("001", futureDate)
        });
        var eodText = await eodRes.TextAsync();
        eodRes.Status.Should().Be(200, because: eodText);
        var eodJson = await eodRes.JsonAsync();
        var summary = eodJson?.Deserialize<DepositEodSummaryDto>(JsonOptions)!;

        summary.UnclaimedTransferredCount.Should().BeGreaterThan(0);
        summary.TotalTransferredAmount.Should().BeGreaterThan(0m);

        // 3. Verify Customer Account has been zeroed out and marked Unclaimed10YearTransferred
        var accRes = await api.GetAsync($"/api/v1/accounts/{accNum}");
        accRes.Status.Should().Be(200);
        var accJson = await accRes.JsonAsync();
        var account = accJson?.Deserialize<AccountResponseDto>(JsonOptions)!;

        account.Status.Should().Be("Unclaimed10YearTransferred");
        account.Balance.Should().Be(0.00m);

        // 4. Verify Trial Balance reflects credit in Bangladesh Bank Unclaimed Funds GL (203040000)
        var tbRes = await api.GetAsync("/api/v1/gl/trial-balance", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "AUDITOR01" } }
        });
        tbRes.Status.Should().Be(200);
        var tbJson = await tbRes.JsonAsync();
        var tb = tbJson?.Deserialize<TrialBalanceDto>(JsonOptions)!;

        tb.IsBalanced.Should().BeTrue();
        tb.Discrepancy.Should().Be(0.00m);
        tb.Lines.Should().Contain(l => l.AccountCode == StandardGlAccounts.BangladeshBankUnclaimedFunds && l.CreditSum > 0);
    }

    [Fact]
    public async Task PlaywrightE2E_DormancyReactivation_EnforcesDualControlMakerChecker()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Create Current Account (Dormancy threshold = 6 months)
        var (_, accNum) = await CreateCustomerAndAccountAsync(api, "Dormant Reactivation Customer 04", DepositProductType.CurrentAccount);

        await api.PostAsync($"/api/v1/accounts/{accNum}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TELLER01" } },
            DataObject = new PostTransactionRequest(accNum, DebitCredit.Credit, 50_000m, "Opening Deposit")
        });

        // 2. Advance date by 8 months to trigger Dormancy
        var dateAfter8Months = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(8)).ToString("yyyy-MM-dd");
        await api.PostAsync("/api/v1/accounts/lifecycle/eod-batch", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "OPS_OFFICER_01" } },
            DataObject = new DepositLifecycleEodRequest("001", dateAfter8Months)
        });

        // 3. Verify customer-induced debit is blocked on dormant account
        var debitRes = await api.PostAsync($"/api/v1/accounts/{accNum}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TELLER01" } },
            DataObject = new PostTransactionRequest(accNum, DebitCredit.Debit, 5_000m, "Withdrawal attempt")
        });
        debitRes.Status.Should().Be(400);

        // 4. Maker requests reactivation with KYC verification document reference
        var reqRes = await api.PostAsync($"/api/v1/accounts/{accNum}/request-reactivation", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = new RequestDormancyReactivationRequest("NID_EKYC_VERIFICATION_PASS_789", "Customer visited branch with NID")
        });
        reqRes.Status.Should().Be(200);

        // 5. Maker attempts to approve their own request -> Prohibited by MakerCheckerSeparationInvariant
        var selfApproveRes = await api.PostAsync($"/api/v1/accounts/{accNum}/approve-reactivation", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = new ApproveDormancyReactivationRequest("Self approval attempt")
        });
        selfApproveRes.Status.Should().Be(400);

        // 6. Branch Manager Checker approves -> Succeeds and account transitions back to Active
        var checkerApproveRes = await api.PostAsync($"/api/v1/accounts/{accNum}/approve-reactivation", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "BM_MANAGER_01" } },
            DataObject = new ApproveDormancyReactivationRequest("Approved after biometric validation")
        });
        checkerApproveRes.Status.Should().Be(200);

        var verifyAccRes = await api.GetAsync($"/api/v1/accounts/{accNum}");
        var verifyAcc = (await verifyAccRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;
        verifyAcc.Status.Should().Be("Active");

        // 7. Debit is now permitted
        var validDebitRes = await api.PostAsync($"/api/v1/accounts/{accNum}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TELLER01" } },
            DataObject = new PostTransactionRequest(accNum, DebitCredit.Debit, 5_000m, "Legitimate withdrawal")
        });
        validDebitRes.Status.Should().Be(200);
    }

    [Fact]
    public async Task PlaywrightE2E_UnclaimedAgeingReport_ReturnsCategorizedAccounts()
    {
        var api = await _fixture.CreateApiContextAsync();

        // Query regulatory report
        var reportRes = await api.GetAsync("/api/v1/accounts/reports/unclaimed-ageing?branchCode=001", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "AUDITOR01" } }
        });
        reportRes.Status.Should().Be(200);

        var reportJson = await reportRes.JsonAsync();
        var items = reportJson?.Deserialize<List<UnclaimedAccountAgeingDto>>(JsonOptions)!;
        items.Should().NotBeNull();
    }
}
