using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

// ARCHITECTURAL INTENT: End-to-End integration tests asserting Customer Account Statements (On-Demand & Periodic),
// Ad-Hoc/Duplicate Statement Statutory Fee deduction (BDT 100 + 15% VAT = BDT 115) and fee waiver for periodic cycles (BRPD 3.1.17, 3.1.23),
// Balance Confirmation Certificate with unencumbered available balance disclosure and fee deduction (BRPD 3.1.17-iii),
// NBR Section 102/53F Annual Withholding Tax (AIT) Certificate (10% with TIN, 15% without TIN) (BRPD 3.1.19),
// SWIFT MT940/MT942 Message Generation with ISO 15022 balance checksums (BRPD 3.1.17-iv),
// and Mandatory Statutory Document Expiration Engine with 30-day grace period, automated Debit Block restraint placement,
// and dual-control renewal unblocking (BRPD 8.1.8-d, 8.1.15, 1.6.6).
public class AccountStatementsAndCompliancePlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public AccountStatementsAndCompliancePlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(string Cif, string AccountNumber)> CreateCustomerAndAccountWithBalanceAsync(
        IAPIRequestContext api,
        decimal initialBalance = 100_000m,
        DepositProductType productType = DepositProductType.SavingsAccount,
        bool hasTin = true)
    {
        string uniqueNid = $"9{Random.Shared.Next(100000000, 999999999)}";
        var draftReq = new CreateCustomerDraftRequest(
            CustomerType.Individual,
            "Statement Compliance Customer",
            new DateOnly(1990, 6, 15),
            "Male",
            "Father Name",
            "Mother Name",
            "Gulshan Avenue",
            "Gulshan",
            "Dhaka",
            "1212",
            "+8801711223399",
            "0101",
            uniqueNid,
            hasTin ? "123456789012" : null);

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
            HasTin: hasTin);

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
    public async Task AdHocStatement_DebitsStatutoryFeeAndVat_RecordedInLedger_BRPD_3_1_23()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNo) = await CreateCustomerAndAccountWithBalanceAsync(api, 50_000m);

        // 1. Post a customer transaction
        var postReq = new PostTransactionRequest(accNo, DebitCredit.Credit, 15_000m, "Customer Advance Deposit");
        var postRes = await api.PostAsync($"/api/v1/accounts/{accNo}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01", ["X-Branch-Code"] = "0101" },
            DataObject = postReq
        });
        postRes.Status.Should().Be(200);

        // 2. Request On-Demand Ad-Hoc Statement (BDT 100 Fee + 15% VAT = BDT 115)
        var stmtReq = new GenerateStatementRequest(
            AccountNumber: accNo,
            FromDate: new DateOnly(2026, 1, 1),
            ToDate: new DateOnly(2026, 11, 12),
            StatementType: AccountStatementType.AdHocDemand,
            WaiveFee: false);

        var stmtRes = await api.PostAsync($"/api/v1/accounts/{accNo}/statements/generate", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" },
            DataObject = stmtReq
        });
        stmtRes.Status.Should().Be(200);
        var stmt = (await stmtRes.JsonAsync())?.Deserialize<AccountStatementDto>(JsonOptions)!;

        stmt.AccountNumber.Should().Be(accNo);
        stmt.FeeDeducted.Should().Be(100.00m);
        stmt.VatDeducted.Should().Be(15.00m);
        stmt.StatementType.Should().Be(AccountStatementType.AdHocDemand.ToString());
        stmt.Transactions.Should().NotBeEmpty();

        // 3. Verify Account Balance after BDT 115 debit: 50,000 + 15,000 - 115 = 64,885
        var balRes = await api.GetAsync($"/api/v1/accounts/{accNo}/balance-summary", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" }
        });
        balRes.Status.Should().Be(200);
        var bal = (await balRes.JsonAsync())?.Deserialize<AccountBalanceSummaryDto>(JsonOptions)!;
        bal.LedgerBalance.Should().Be(64_885.00m);
    }

    [Fact]
    public async Task PeriodicStatement_IsStrictlyFreeOfCharge_BRPD_3_1_17_ii()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNo) = await CreateCustomerAndAccountWithBalanceAsync(api, 30_000m);

        // Request Scheduled Periodic Half-Yearly Statement: Strictly Free
        var stmtReq = new GenerateStatementRequest(
            AccountNumber: accNo,
            FromDate: new DateOnly(2026, 1, 1),
            ToDate: new DateOnly(2026, 6, 30),
            StatementType: AccountStatementType.PeriodicScheduled,
            WaiveFee: false);

        var stmtRes = await api.PostAsync($"/api/v1/accounts/{accNo}/statements/generate", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "SYSTEM_BATCH" },
            DataObject = stmtReq
        });
        stmtRes.Status.Should().Be(200);
        var stmt = (await stmtRes.JsonAsync())?.Deserialize<AccountStatementDto>(JsonOptions)!;

        stmt.FeeDeducted.Should().Be(0m);
        stmt.VatDeducted.Should().Be(0m);

        // Verify Balance Unchanged
        var balRes = await api.GetAsync($"/api/v1/accounts/{accNo}/balance-summary", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" }
        });
        balRes.Status.Should().Be(200);
        var bal = (await balRes.JsonAsync())?.Deserialize<AccountBalanceSummaryDto>(JsonOptions)!;
        bal.LedgerBalance.Should().Be(30_000.00m);
    }

    [Fact]
    public async Task BalanceCertificate_DisclosesActiveHolds_DebitsStatutoryFee_BRPD_3_1_17_iii()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNo) = await CreateCustomerAndAccountWithBalanceAsync(api, 150_000m);

        // 1. Place a BDT 50,000 lien hold
        var placeHoldReq = new PlaceRestraintRequest(
            RestraintType: "GeneralLien",
            Scope: "PartialAmountHold",
            HoldAmount: 50_000m,
            ReasonCategory: "SecurityHold",
            NarrativeReason: "Lien hold against LC liability",
            IssuingAuthority: "Trade Finance Department",
            ReferenceCaseNo: "TF-LC-2026-88",
            MakerUserId: "MAKER01",
            CheckerUserId: "CHECKER01");

        var placeRes = await api.PostAsync($"/api/v1/accounts/{accNo}/restraints/place", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = placeHoldReq
        });
        placeRes.Status.Should().Be(201);

        // 2. Generate Balance Confirmation Certificate
        var certReq = new GenerateBalanceCertificateRequest(
            AccountNumber: accNo,
            AsOfDate: new DateOnly(2026, 11, 12),
            RequestedByUserId: "OFFICER01",
            WaiveFee: false);

        var certRes = await api.PostAsync($"/api/v1/accounts/{accNo}/statements/balance-certificate", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "OFFICER01" },
            DataObject = certReq
        });
        certRes.Status.Should().Be(200);
        var cert = (await certRes.JsonAsync())?.Deserialize<BalanceConfirmationCertificateDto>(JsonOptions)!;

        cert.AccountNumber.Should().Be(accNo);
        cert.CertificateNumber.Should().StartWith("CERT-BAL-");
        cert.TotalActiveHoldAmount.Should().Be(50_000.00m);
        cert.FeeDeducted.Should().Be(100.00m);
        cert.VatDeducted.Should().Be(15.00m);
        cert.CertificationText.Should().Contain("unencumbered available balance");
    }

    [Fact]
    public async Task TaxCertificate_NBR_Section102_53F_WithholdingRateAndChallan_BRPD_3_1_19()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNo) = await CreateCustomerAndAccountWithBalanceAsync(api, 200_000m, hasTin: true);

        // Generate NBR Tax Certificate
        var certReq = new GenerateTaxCertificateRequest(
            AccountNumber: accNo,
            FiscalYearStart: 2025,
            RequestedByUserId: "OFFICER01");

        var certRes = await api.PostAsync($"/api/v1/accounts/{accNo}/statements/tax-certificate", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "OFFICER01" },
            DataObject = certReq
        });
        certRes.Status.Should().Be(200);
        var cert = (await certRes.JsonAsync())?.Deserialize<TaxDeductionCertificateDto>(JsonOptions)!;

        cert.AccountNumber.Should().Be(accNo);
        cert.CertificateNumber.Should().StartWith("CERT-TAX-2025-");
        cert.FiscalYearStart.Should().Be(2025);
        cert.FiscalYearEnd.Should().Be(2026);
        cert.TaxRatePercentage.Should().Be(10.00m); // 10% rate for verified TIN
        cert.GovernmentTreasuryChallanRef.Should().Contain("NBR-TR-2025");
    }

    [Fact]
    public async Task CustomerDocumentCompliance_ExpiryScan_PlacesDebitBlockRestraint_AndDualControlRenewUnblocks_BRPD_8_1_8_d()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (cif, accNo) = await CreateCustomerAndAccountWithBalanceAsync(api, 100_000m);

        // 1. Register an expired Trade License (>30 days expired)
        var docReq = new RegisterCustomerDocumentRequest(
            cif,
            CrmDocumentCategory.TradeLicense,
            $"TL-{Random.Shared.Next(10000, 99999)}",
            new DateOnly(2024, 6, 30),
            new DateOnly(2025, 6, 30), // Expired over a year ago (>30 days grace period)
            "Dhaka North City Corporation");

        var docRes = await api.PostAsync("/api/v1/crm/documents/register", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = docReq
        });
        docRes.Status.Should().Be(201);
        var doc = (await docRes.JsonAsync())?.Deserialize<CustomerDocumentResponse>(JsonOptions)!;

        // 2. Trigger Compliance Document Expiration Scan Batch
        var scanReq = new ScanExpiredDocumentsRequest(
            AsOfDate: new DateOnly(2026, 11, 12),
            EvaluatorUserId: "COMPLIANCE_OPS01");

        var scanRes = await api.PostAsync("/api/v1/customers/compliance/documents/scan-expired", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "COMPLIANCE_OPS01" },
            DataObject = scanReq
        });
        scanRes.Status.Should().Be(200);
        var scanSummary = (await scanRes.JsonAsync())?.Deserialize<ExpiredDocumentScanSummaryDto>(JsonOptions)!;
        scanSummary.GracePeriodBreachedCount.Should().BeGreaterThanOrEqualTo(1);
        scanSummary.RestraintsAppliedCount.Should().BeGreaterThanOrEqualTo(1);

        // 3. Verify Account is now Debit-Blocked
        var balRes = await api.GetAsync($"/api/v1/accounts/{accNo}/balance-summary", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" }
        });
        balRes.Status.Should().Be(200);
        var bal = (await balRes.JsonAsync())?.Deserialize<AccountBalanceSummaryDto>(JsonOptions)!;
        bal.OperatingStatus.Should().Be(AccountOperatingStatus.BlockedDebit.ToString());

        // 4. Renew Document under Dual Control (Maker: MAKER01, Checker: CHECKER02)
        var renewReq = new RenewDocumentRequest(
            NewDocumentNumber: $"TL-{Random.Shared.Next(10000, 99999)}-RENEWED",
            NewIssueDate: new DateOnly(2026, 7, 1),
            NewExpiryDate: new DateOnly(2027, 6, 30),
            MakerUserId: "MAKER01",
            CheckerUserId: "CHECKER02");

        var renewRes = await api.PostAsync($"/api/v1/customers/compliance/documents/{doc.Id}/renew", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = renewReq
        });
        renewRes.Status.Should().Be(200);
        var renewDto = (await renewRes.JsonAsync())?.Deserialize<RenewDocumentResponseDto>(JsonOptions)!;
        renewDto.IsRestraintReleased.Should().BeTrue();

        // 5. Verify Account Status restored to Active
        var restoredBalRes = await api.GetAsync($"/api/v1/accounts/{accNo}/balance-summary", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" }
        });
        restoredBalRes.Status.Should().Be(200);
        var restoredBal = (await restoredBalRes.JsonAsync())?.Deserialize<AccountBalanceSummaryDto>(JsonOptions)!;
        restoredBal.OperatingStatus.Should().Be(AccountOperatingStatus.Active.ToString());
    }

    [Fact]
    public async Task SwiftStatement_MT940_And_MT942_FormatsIso15022ChecksumTags_BRPD_3_1_17_iv()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNo) = await CreateCustomerAndAccountWithBalanceAsync(api, 120_000m, DepositProductType.CurrentAccount);

        // Generate SWIFT MT940 Customer Statement
        var mt940Req = new GenerateSwiftStatementRequest(
            AccountNumber: accNo,
            FromDate: new DateOnly(2026, 1, 1),
            ToDate: new DateOnly(2026, 11, 12),
            StatementType: SwiftStatementType.MT940_CustomerStatement,
            StatementSequenceNumber: 10);

        var mt940Res = await api.PostAsync($"/api/v1/accounts/{accNo}/statements/swift-mt940", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TREASURY01" },
            DataObject = mt940Req
        });
        mt940Res.Status.Should().Be(200);
        var mt940 = (await mt940Res.JsonAsync())?.Deserialize<SwiftStatementResponseDto>(JsonOptions)!;

        mt940.StatementType.Should().Be(SwiftStatementType.MT940_CustomerStatement.ToString());
        mt940.SwiftMessageBlock.Should().Contain(":20:STMT20261112010");
        mt940.SwiftMessageBlock.Should().Contain($":25:{accNo}");
        mt940.SwiftMessageBlock.Should().Contain(":28C:00010/1");
        mt940.SwiftMessageBlock.Should().Contain(":60F:");
        mt940.SwiftMessageBlock.Should().Contain(":62F:");
        mt940.SwiftMessageBlock.Should().EndWith("-}");

        // Generate SWIFT MT942 Interim Transaction Report
        var mt942Req = new GenerateSwiftStatementRequest(
            AccountNumber: accNo,
            FromDate: new DateOnly(2026, 11, 12),
            ToDate: new DateOnly(2026, 11, 12),
            StatementType: SwiftStatementType.MT942_InterimTransactionReport,
            StatementSequenceNumber: 2);

        var mt942Res = await api.PostAsync($"/api/v1/accounts/{accNo}/statements/swift-mt942", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TREASURY01" },
            DataObject = mt942Req
        });
        mt942Res.Status.Should().Be(200);
        var mt942 = (await mt942Res.JsonAsync())?.Deserialize<SwiftStatementResponseDto>(JsonOptions)!;

        mt942.StatementType.Should().Be(SwiftStatementType.MT942_InterimTransactionReport.ToString());
        mt942.SwiftMessageBlock.Should().Contain(":20:ITR20261112002");
        mt942.SwiftMessageBlock.Should().Contain(":34F:");
        mt942.SwiftMessageBlock.Should().EndWith("-}");
    }
}
