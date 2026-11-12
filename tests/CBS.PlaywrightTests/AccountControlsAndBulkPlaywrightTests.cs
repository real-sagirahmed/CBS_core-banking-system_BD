using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Corporate.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

// ARCHITECTURAL INTENT: End-to-End integration tests asserting Account Restraints & Liens (BRPD 1.6.2-v, 1.6.6, 3.1.15),
// Statutory Account Closure Lifecycle & Invariants (BRPD 1.6.4, 3.1.19),
// and High-Throughput Bulk Corporate Salary/Dividend Processing (BRPD 1.6.16)
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024).
public class AccountControlsAndBulkPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public AccountControlsAndBulkPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(string Cif, string AccountNumber)> CreateCustomerAndAccountWithBalanceAsync(
        IAPIRequestContext api,
        decimal initialBalance = 100000m,
        DepositProductType productType = DepositProductType.SavingsAccount)
    {
        string uniqueNid = $"9{Random.Shared.Next(100000000, 999999999)}";
        var draftReq = new CreateCustomerDraftRequest(
            CustomerType.Individual,
            "Account Control Test Customer",
            new DateOnly(1992, 4, 10),
            "Male",
            "Father Name",
            "Mother Name",
            "Gulshan Avenue",
            "Gulshan",
            "Dhaka",
            "1212",
            "+8801811223344",
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

        var openAccReq = new OpenDepositAccountRequest(
            customer.CifNumber,
            productType,
            "0101",
            InitialDeposit: initialBalance,
            HasTin: true);

        var openAccRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01", ["X-Branch-Code"] = "0101" },
            DataObject = openAccReq
        });
        openAccRes.Status.Should().Be(201);
        var account = (await openAccRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;

        return (customer.CifNumber, account.AccountNumber);
    }

    [Fact]
    public async Task AccountRestraints_PlacePartialHold_EnforcesAvailableBalance_And_ReleasesSuccessfully()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNo) = await CreateCustomerAndAccountWithBalanceAsync(api, 200_000m);

        // 1. Place Partial Amount Hold of BDT 80,000 (BRPD 3.1.15-ix, x)
        var placeHoldReq = new PlaceRestraintRequest(
            RestraintType: "GeneralLien",
            Scope: "PartialAmountHold",
            HoldAmount: 80_000m,
            ReasonCategory: "LoanDefaultLien",
            NarrativeReason: "Lien against overdue liability in Branch 0101",
            IssuingAuthority: "Credit Risk Management Division",
            ReferenceCaseNo: "CRM-LN-2026-101",
            MakerUserId: "MAKER01",
            CheckerUserId: "CHECKER01");

        var placeRes = await api.PostAsync($"/api/v1/accounts/{accNo}/restraints/place", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = placeHoldReq
        });
        placeRes.Status.Should().Be(201);
        var restraint = (await placeRes.JsonAsync())?.Deserialize<AccountRestraintResponseDto>(JsonOptions)!;
        restraint.HoldAmount.Should().Be(80_000m);
        restraint.Status.Should().Be("Active");

        // 2. Inquire Balance Summary: Ledger=200,000, Hold=80,000, Available=120,000
        var balRes = await api.GetAsync($"/api/v1/accounts/{accNo}/balance-summary", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" }
        });
        balRes.Status.Should().Be(200);
        var balSummary = (await balRes.JsonAsync())?.Deserialize<AccountBalanceSummaryDto>(JsonOptions)!;
        balSummary.LedgerBalance.Should().Be(200_000m);
        balSummary.TotalActiveHoldAmount.Should().Be(80_000m);
        balSummary.AvailableBalance.Should().Be(120_000m);
        balSummary.ActiveRestraintsCount.Should().Be(1);

        // 3. Attempt Debit that breaches Available Balance (150,000 > 120,000) -> MUST FAIL (BRPD 3.1.15-ix)
        var failDebitReq = new PostTransactionRequest(accNo, DebitCredit.Debit, 150_000m, "Attempted Over-Lien Debit");
        var failDebitRes = await api.PostAsync($"/api/v1/accounts/{accNo}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01", ["X-Branch-Code"] = "0101" },
            DataObject = failDebitReq
        });
        failDebitRes.Status.Should().Be(400);

        // 4. Debit within Available Balance (100,000 <= 120,000) -> SUCCEEDS
        var succDebitReq = new PostTransactionRequest(accNo, DebitCredit.Debit, 100_000m, "Permissible Cash Withdrawal");
        var succDebitRes = await api.PostAsync($"/api/v1/accounts/{accNo}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01", ["X-Branch-Code"] = "0101" },
            DataObject = succDebitReq
        });
        succDebitRes.Status.Should().Be(200);

        // Verify remaining available balance is 20,000
        var balAfterDebitRes = await api.GetAsync($"/api/v1/accounts/{accNo}/balance-summary", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" }
        });
        var balAfterDebit = (await balAfterDebitRes.JsonAsync())?.Deserialize<AccountBalanceSummaryDto>(JsonOptions)!;
        balAfterDebit.LedgerBalance.Should().Be(100_000m);
        balAfterDebit.AvailableBalance.Should().Be(20_000m);

        // 5. Release Restraint (Maker/Checker dual-control)
        var releaseReq = new ReleaseRestraintRequest("MAKER01", "CHECKER01", "Liability cleared by customer");
        var releaseRes = await api.PostAsync($"/api/v1/accounts/{accNo}/restraints/{restraint.RestraintId}/release", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = releaseReq
        });
        releaseRes.Status.Should().Be(200);

        // Available balance is restored to full ledger balance
        var balAfterReleaseRes = await api.GetAsync($"/api/v1/accounts/{accNo}/balance-summary", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" }
        });
        var balAfterRelease = (await balAfterReleaseRes.JsonAsync())?.Deserialize<AccountBalanceSummaryDto>(JsonOptions)!;
        balAfterRelease.TotalActiveHoldAmount.Should().Be(0m);
        balAfterRelease.AvailableBalance.Should().Be(100_000m);
    }

    [Fact]
    public async Task AccountRestraints_CourtAttachment_FreezesAccount_And_RejectsTransactions()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNo) = await CreateCustomerAndAccountWithBalanceAsync(api, 100_000m);

        // Place Total Freeze Court Attachment Order (BRPD 1.6.2-v, 3.1.15-i)
        var freezeReq = new PlaceRestraintRequest(
            RestraintType: "CourtAttachmentOrder",
            Scope: "TotalFreeze",
            HoldAmount: 0m,
            ReasonCategory: "CourtWrit",
            NarrativeReason: "Subordinate Judge Court Seizure Order",
            IssuingAuthority: "Dhaka 3rd Joint District Judge Court",
            ReferenceCaseNo: "MISC-CASE-456/2026",
            MakerUserId: "MAKER01",
            CheckerUserId: "CHECKER01");

        var freezeRes = await api.PostAsync($"/api/v1/accounts/{accNo}/restraints/place", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = freezeReq
        });
        freezeRes.Status.Should().Be(201);

        // Verify status is Frozen
        var balRes = await api.GetAsync($"/api/v1/accounts/{accNo}/balance-summary", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" }
        });
        var summary = (await balRes.JsonAsync())?.Deserialize<AccountBalanceSummaryDto>(JsonOptions)!;
        summary.OperatingStatus.Should().Be("Frozen");

        // Attempt transaction fails with 400
        var debitReq = new PostTransactionRequest(accNo, DebitCredit.Debit, 5_000m, "Withdrawal on frozen account");
        var debitRes = await api.PostAsync($"/api/v1/accounts/{accNo}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01", ["X-Branch-Code"] = "0101" },
            DataObject = debitReq
        });
        debitRes.Status.Should().Be(400);
    }

    [Fact]
    public async Task AccountClosing_WithActiveHold_Rejects_And_OnceClean_ClosesSuccessfullyWithStatutoryFee()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNo) = await CreateCustomerAndAccountWithBalanceAsync(api, 50_000m);

        // Place hold
        var holdReq = new PlaceRestraintRequest(
            "SecurityDepositLien",
            "PartialAmountHold",
            10_000m,
            "TenderSecurity",
            "Tender earnest money hold",
            "Public Works Department",
            "PWD-TEND-2026",
            "MAKER01",
            "CHECKER01");

        var holdRes = await api.PostAsync($"/api/v1/accounts/{accNo}/restraints/place", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = holdReq
        });
        holdRes.Status.Should().Be(201);
        var restraint = (await holdRes.JsonAsync())?.Deserialize<AccountRestraintResponseDto>(JsonOptions)!;

        // 1. Get Closing Quote: Should indicate CanClose = false
        var quoteRes = await api.GetAsync($"/api/v1/accounts/{accNo}/close/quote", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" }
        });
        quoteRes.Status.Should().Be(200);
        var quote = (await quoteRes.JsonAsync())?.Deserialize<AccountClosingQuoteDto>(JsonOptions)!;
        quote.CanClose.Should().BeFalse();
        quote.ValidationErrors.Should().Contain(e => e.Contains("active restraint holds"));

        // 2. Direct attempt to close must fail with 400 (ActiveRestraintsHoldInvariant)
        var closeReq = new CloseAccountRequest("MAKER01", "CHECKER01", "Customer Request", "CashPayout");
        var failCloseRes = await api.PostAsync($"/api/v1/accounts/{accNo}/close", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = closeReq
        });
        failCloseRes.Status.Should().Be(400);

        // 3. Release restraint
        var releaseRes = await api.PostAsync($"/api/v1/accounts/{accNo}/restraints/{restraint.RestraintId}/release", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = new ReleaseRestraintRequest("MAKER01", "CHECKER01", "Tender awarded and completed")
        });
        releaseRes.Status.Should().Be(200);

        // 4. Quote now succeeds: BDT 300 fee + BDT 45 VAT = 345 deduction, refund 49,655
        var cleanQuoteRes = await api.GetAsync($"/api/v1/accounts/{accNo}/close/quote", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" }
        });
        cleanQuoteRes.Status.Should().Be(200);
        var cleanQuote = (await cleanQuoteRes.JsonAsync())?.Deserialize<AccountClosingQuoteDto>(JsonOptions)!;
        cleanQuote.CanClose.Should().BeTrue();
        cleanQuote.ClosingFee.Should().Be(300.00m);
        cleanQuote.VatAmount.Should().Be(45.00m);
        cleanQuote.NetRefundAmount.Should().Be(49_655.00m);

        // 5. Execute Account Closure
        var succCloseRes = await api.PostAsync($"/api/v1/accounts/{accNo}/close", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = closeReq
        });
        succCloseRes.Status.Should().Be(200);
        var closeResult = (await succCloseRes.JsonAsync())?.Deserialize<AccountClosingResponseDto>(JsonOptions)!;
        closeResult.Status.Should().Be("Closed");
        closeResult.ClosingFeeDeducted.Should().Be(300.00m);
        closeResult.VatDeducted.Should().Be(45.00m);
        closeResult.NetSettlementAmount.Should().Be(49_655.00m);
    }

    [Fact]
    public async Task BulkCorporatePayroll_UploadValidateAndExecute_CreditsEmployeesAndReportsSummary()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Create Corporate Employer Account with BDT 500,000
        var (_, corpAccNo) = await CreateCustomerAndAccountWithBalanceAsync(api, 500_000m, DepositProductType.CurrentAccount);

        // 2. Create 2 Employee Beneficiary Accounts
        var (_, emp1AccNo) = await CreateCustomerAndAccountWithBalanceAsync(api, 10_000m, DepositProductType.SavingsAccount);
        var (_, emp2AccNo) = await CreateCustomerAndAccountWithBalanceAsync(api, 15_000m, DepositProductType.SavingsAccount);

        // 3. Upload Corporate Payroll Batch (BRPD 1.6.16)
        var uploadReq = new UploadBulkBatchRequest(
            BatchType: "SalaryDisbursement",
            CorporateDebitAccountNumber: corpAccNo,
            Narration: "TechCorp November 2026 Executive Payroll",
            ValueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            MakerUserId: "CORP_MAKER01",
            Items:
            [
                new UploadBulkItemDto(emp1AccNo, "Senior Architect", 60_000m, "SAL-001"),
                new UploadBulkItemDto(emp2AccNo, "Principal Engineer", 80_000m, "SAL-002")
            ]);

        var uploadRes = await api.PostAsync("/api/v1/corporate/bulk-batches/upload", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CORP_MAKER01" },
            DataObject = uploadReq
        });
        uploadRes.Status.Should().Be(201);
        var batchSummary = (await uploadRes.JsonAsync())?.Deserialize<BulkBatchSummaryDto>(JsonOptions)!;
        batchSummary.TotalRecords.Should().Be(2);
        batchSummary.TotalAmount.Should().Be(140_000m);
        batchSummary.Status.Should().Be("Validated");

        // 4. Execute Bulk Batch
        var execReq = new ExecuteBulkBatchRequest("CORP_CHECKER02");
        var execRes = await api.PostAsync($"/api/v1/corporate/bulk-batches/{batchSummary.BatchId}/execute", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CORP_CHECKER02" },
            DataObject = execReq
        });
        execRes.Status.Should().Be(200);
        var execResult = (await execRes.JsonAsync())?.Deserialize<ExecuteBulkBatchResponseDto>(JsonOptions)!;
        execResult.Status.Should().Be("Executed");
        execResult.SuccessCount.Should().Be(2);
        execResult.FailedCount.Should().Be(0);
        execResult.SuccessfulAmount.Should().Be(140_000m);
        execResult.CorporateDebitedAmount.Should().Be(140_000m);

        // 5. Verify Balances Post Execution
        // Corporate Account: 500,000 - 140,000 = 360,000
        var corpBalRes = await api.GetAsync($"/api/v1/accounts/{corpAccNo}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" }
        });
        var corpAccount = (await corpBalRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;
        corpAccount.Balance.Should().Be(360_000m);

        // Employee 1: 10,000 + 60,000 = 70,000
        var emp1BalRes = await api.GetAsync($"/api/v1/accounts/{emp1AccNo}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" }
        });
        var emp1Account = (await emp1BalRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;
        emp1Account.Balance.Should().Be(70_000m);

        // Employee 2: 15,000 + 80,000 = 95,000
        var emp2BalRes = await api.GetAsync($"/api/v1/accounts/{emp2AccNo}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" }
        });
        var emp2Account = (await emp2BalRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;
        emp2Account.Balance.Should().Be(95_000m);
    }
}
