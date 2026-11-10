using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Application.SecurityAudit.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class SecurityAuditCompliancePlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public SecurityAuditCompliancePlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PlaywrightE2E_SessionSecurity_PreventsConcurrentLogins_AndBlocksAdminFinancialTransactions()
    {
        var api = await _fixture.CreateApiContextAsync();
        var uniqueSuffix = Random.Shared.Next(10000, 99999);
        var tellerUser = $"TELLER_{uniqueSuffix}";
        var adminUser = $"ADMIN_{uniqueSuffix}";
        var session1Id = $"SES-TEL-01-{uniqueSuffix}";
        var session2Id = $"SES-TEL-02-{uniqueSuffix}";
        var adminSessionId = $"SES-ADM-01-{uniqueSuffix}";

        // Act 1: Teller logs in from terminal IP 192.168.1.10
        var login1Req = new LoginSessionRequest(
            SessionId: session1Id,
            UserId: tellerUser,
            Role: UserRole.BranchTeller,
            BranchCode: "0101",
            TerminalIp: "192.168.1.10");

        var login1Res = await api.PostAsync("/api/v1/security/sessions/login", new APIRequestContextOptions
        {
            DataObject = login1Req
        });
        login1Res.Status.Should().Be(200);
        var session1 = (await login1Res.JsonAsync())?.Deserialize<UserSessionResponse>(JsonOptions);
        session1.Should().NotBeNull();
        session1!.IsActive.Should().BeTrue();
        session1.UserId.Should().Be(tellerUser);

        // Act 2: Same teller attempts concurrent login from another terminal IP (BRPD 10.3.5-viii)
        var login2Req = new LoginSessionRequest(
            SessionId: session2Id,
            UserId: tellerUser,
            Role: UserRole.BranchTeller,
            BranchCode: "0101",
            TerminalIp: "192.168.1.25");

        var login2Res = await api.PostAsync("/api/v1/security/sessions/login", new APIRequestContextOptions
        {
            DataObject = login2Req
        });
        login2Res.Status.Should().Be(400);
        var errorContent = await login2Res.TextAsync();
        errorContent.Should().Contain("BRPD 10.3.5-viii");
        errorContent.Should().Contain("Multiple/concurrent login is blocked");

        // Act 3: System Admin logs in
        var adminLoginReq = new LoginSessionRequest(
            SessionId: adminSessionId,
            UserId: adminUser,
            Role: UserRole.SystemAdmin,
            BranchCode: "0000",
            TerminalIp: "10.0.0.5");

        var adminLoginRes = await api.PostAsync("/api/v1/security/sessions/login", new APIRequestContextOptions
        {
            DataObject = adminLoginReq
        });
        adminLoginRes.Status.Should().Be(200);

        // Act 4: System Admin attempts to execute or authorize a financial transaction (BRPD 10.3.5-vii)
        var assertFinRes = await api.PostAsync("/api/v1/security/permission/assert-financial", new APIRequestContextOptions
        {
            DataObject = new AssertFinancialPermissionRequest(adminSessionId)
        });
        assertFinRes.Status.Should().Be(400);
        var adminFinError = await assertFinRes.TextAsync();
        adminFinError.Should().Contain("BRPD 10.3.5-vii");
        adminFinError.Should().Contain("Super/Admin users in CBS shall not be authorized to perform any financial transactions");

        // Act 5: Teller 1 logs out gracefully
        var logoutRes = await api.PostAsync("/api/v1/security/sessions/logout", new APIRequestContextOptions
        {
            DataObject = new LogoutSessionRequest(session1Id)
        });
        logoutRes.Status.Should().Be(200);

        // Act 6: Teller can now establish a new active session
        var reLoginRes = await api.PostAsync("/api/v1/security/sessions/login", new APIRequestContextOptions
        {
            DataObject = login2Req
        });
        reLoginRes.Status.Should().Be(200);
        var reLoginSession = (await reLoginRes.JsonAsync())?.Deserialize<UserSessionResponse>(JsonOptions);
        reLoginSession.Should().NotBeNull();
        reLoginSession!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task PlaywrightE2E_ImmutableAuditTrail_CryptographicHashChainIntegrity_AndCaatsExtraction()
    {
        var api = await _fixture.CreateApiContextAsync();
        var uniqueSuffix = Random.Shared.Next(1000, 9999);
        var officerId = $"OFFICER_{uniqueSuffix}";

        // Act 1: Record 3 audit journal events sequentially
        var entry1Req = new LogAuditEntryRequest(
            UserId: officerId,
            Role: UserRole.BranchTeller,
            BranchCode: "0101",
            EventCategory: AuditEventCategory.FinancialTransaction,
            Action: "CustomerDepositAccountCreated",
            TargetEntityName: "DepositAccount",
            TargetEntityId: $"0101101{uniqueSuffix:D6}",
            BeforeStateJson: null,
            AfterStateJson: "{\"AccountType\":\"Savings\",\"Status\":\"Active\"}",
            TerminalIp: "192.168.1.15");

        var entry1Res = await api.PostAsync("/api/v1/audit/log", new APIRequestContextOptions { DataObject = entry1Req });
        entry1Res.Status.Should().Be(200);
        var e1 = (await entry1Res.JsonAsync())?.Deserialize<AuditJournalResponse>(JsonOptions);
        e1.Should().NotBeNull();
        e1!.IsHashValid.Should().BeTrue();

        var entry2Req = new LogAuditEntryRequest(
            UserId: officerId,
            Role: UserRole.CadOfficer,
            BranchCode: "0101",
            EventCategory: AuditEventCategory.ParameterChange,
            Action: "DrawingPowerLimitAssigned",
            TargetEntityName: "LoanAccount",
            TargetEntityId: $"0101401{uniqueSuffix:D6}",
            BeforeStateJson: "{\"Limit\":0}",
            AfterStateJson: "{\"Limit\":5000000}",
            TerminalIp: "192.168.1.18");

        var entry2Res = await api.PostAsync("/api/v1/audit/log", new APIRequestContextOptions { DataObject = entry2Req });
        entry2Res.Status.Should().Be(200);
        var e2 = (await entry2Res.JsonAsync())?.Deserialize<AuditJournalResponse>(JsonOptions);
        e2.Should().NotBeNull();
        e2!.IsHashValid.Should().BeTrue();
        e2.PreviousEntryHash.Should().Be(e1.CurrentEntryHash); // Invariant: Cryptographic chain linking

        var entry3Req = new LogAuditEntryRequest(
            UserId: $"CHECKER_{uniqueSuffix}",
            Role: UserRole.CadOfficer,
            BranchCode: "0101",
            EventCategory: AuditEventCategory.FinancialTransaction,
            Action: "CashWithdrawalAuthorized",
            TargetEntityName: "CashTransaction",
            TargetEntityId: $"TX-{uniqueSuffix}-01",
            BeforeStateJson: "{\"Status\":\"PendingApproval\"}",
            AfterStateJson: "{\"Status\":\"Approved\"}",
            TerminalIp: "192.168.1.20");

        var entry3Res = await api.PostAsync("/api/v1/audit/log", new APIRequestContextOptions { DataObject = entry3Req });
        entry3Res.Status.Should().Be(200);
        var e3 = (await entry3Res.JsonAsync())?.Deserialize<AuditJournalResponse>(JsonOptions);
        e3.Should().NotBeNull();
        e3!.IsHashValid.Should().BeTrue();
        e3.PreviousEntryHash.Should().Be(e2.CurrentEntryHash); // Invariant: Cryptographic chain linking

        // Act 2: Extract audit logs for Computer Assisted Audit Techniques (CAATs)
        var caatsRes = await api.GetAsync("/api/v1/audit/caats-extract");
        caatsRes.Status.Should().Be(200);
        var caatsData = (await caatsRes.JsonAsync())?.Deserialize<CaatsAuditExtractResponse>(JsonOptions);
        caatsData.Should().NotBeNull();
        caatsData!.IsChainIntegrityValid.Should().BeTrue();
        caatsData.TotalRecords.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task PlaywrightE2E_BfiuReporting_GeneratesDailyCtr_AndFlagsStructuringStrAlerts()
    {
        var api = await _fixture.CreateApiContextAsync();
        var uniqueSuffix = Random.Shared.Next(1000, 9999);
        var ctrRef = $"CTR-2026-07-{uniqueSuffix}";
        var strRef = $"STR-2026-07-{uniqueSuffix}";
        var reportDate = new DateTime(2026, 7, 1);

        // Act 1: Generate daily Cash Transaction Report (CTR)
        // Customer 1: 1,800,000 BDT in 3 cash deposits (BFIU threshold: >= 10 lac / 1,000,000 BDT)
        // Customer 2: 750,000 BDT in 2 cash deposits (Below threshold: must NOT be reported)
        var ctrReq = new GenerateCtrReportRequest(
            ReportReference: ctrRef,
            ReportDate: reportDate,
            Aggregates: new List<CustomerCashAggregateInput>
            {
                new("CIF001", "Rahman Traders", 3, 1800000m),
                new("CIF002", "Chowdhury Textiles", 2, 750000m)
            },
            GeneratedByUserId: "COMPLIANCE_OFFICER_01");

        var ctrRes = await api.PostAsync("/api/v1/compliance/bfiu/ctr/generate", new APIRequestContextOptions
        {
            DataObject = ctrReq
        });
        ctrRes.Status.Should().Be(200);
        var ctrReport = (await ctrRes.JsonAsync())?.Deserialize<BfiuReportResponse>(JsonOptions);
        ctrReport.Should().NotBeNull();
        ctrReport!.ReportType.Should().Be("CTR");
        ctrReport.TotalItems.Should().Be(1);
        ctrReport.CtrItems.Should().HaveCount(1);
        ctrReport.CtrItems![0].CifNumber.Should().Be("CIF001");
        ctrReport.CtrItems[0].TotalCashAmount.Should().Be(1800000m);

        // Act 2: Evaluate and lodge Suspicious Transaction Report (STR/SAR) for structuring
        var strReq = new EvaluateStrAlertsRequest(
            ReportReference: strRef,
            ReportDate: reportDate,
            Alerts: new List<StrAlertInput>
            {
                new(
                    CifNumber: "CIF009",
                    CustomerName: "Suspicious Individual",
                    Reason: StrAlertReason.StructuringSmurfing,
                    TransactionAmount: 980000m,
                    Currency: "BDT",
                    Narrative: "Multiple branch cash deposits placed just below 10 lac threshold within 2 hours.")
            },
            GeneratedByUserId: "AML_OFFICER_01");

        var strRes = await api.PostAsync("/api/v1/compliance/bfiu/str/evaluate", new APIRequestContextOptions
        {
            DataObject = strReq
        });
        strRes.Status.Should().Be(200);
        var strReport = (await strRes.JsonAsync())?.Deserialize<BfiuReportResponse>(JsonOptions);
        strReport.Should().NotBeNull();
        strReport!.ReportType.Should().Be("STR");
        strReport.Status.Should().Be("Generated");
        strReport.TotalItems.Should().Be(1);
        strReport.StrAlerts.Should().HaveCount(1);
        strReport.StrAlerts![0].CifNumber.Should().Be("CIF009");
        strReport.StrAlerts[0].Reason.Should().Be("StructuringSmurfing");
    }

    [Fact]
    public async Task PlaywrightE2E_FinancialEndpoint_RejectsSystemAdmin_WithForbidden403_BRPD10_3_5_vii()
    {
        var api = await _fixture.CreateApiContextAsync();
        var uniqueSuffix = Random.Shared.Next(10000, 99999);
        var adminUser = $"ADMIN_PROHIBITED_{uniqueSuffix}";
        var sessionId = $"SES-ADM-FIN-{uniqueSuffix}";

        // 1. Admin logs in and receives cryptographically signed JWT Bearer token
        var loginReq = new LoginSessionRequest(
            SessionId: sessionId,
            UserId: adminUser,
            Role: UserRole.SystemAdmin,
            BranchCode: "0000",
            TerminalIp: "10.0.0.1");

        var loginRes = await api.PostAsync("/api/v1/security/sessions/login", new APIRequestContextOptions
        {
            DataObject = loginReq
        });
        loginRes.Status.Should().Be(200);
        var session = (await loginRes.JsonAsync())?.Deserialize<UserSessionResponse>(JsonOptions);
        session.Should().NotBeNull();
        session!.Token.Should().NotBeNullOrWhiteSpace();

        // 2. Admin attempts financial deposit/transaction using Bearer token (BRPD 10.3.5-vii)
        var txReq = new PostTransactionRequest(
            AccountNumber: "0101101000015",
            Type: DebitCredit.Credit,
            Amount: 50000m,
            Narration: "Unauthorized Admin Deposit");

        var finRes = await api.PostAsync("/api/v1/accounts/0101101000015/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {session.Token}"
            },
            DataObject = txReq
        });

        // 3. Must be strictly rejected with HTTP 403 Forbidden
        finRes.Status.Should().Be(403);
        var errorBody = await finRes.TextAsync();
        errorBody.Should().Contain("BRPD_10_3_5_VII");
        errorBody.Should().Contain("shall not be authorized to perform");
    }

    [Fact]
    public async Task PlaywrightE2E_FinancialEndpoint_AllowsBranchTeller_WithSuccess200()
    {
        var api = await _fixture.CreateApiContextAsync();
        var uniqueSuffix = Random.Shared.Next(10000, 99999);
        var tellerUser = $"TELLER_AUTH_{uniqueSuffix}";
        var sessionId = $"SES-TEL-FIN-{uniqueSuffix}";

        // 1. Teller logs in and receives JWT Bearer token
        var loginReq = new LoginSessionRequest(
            SessionId: sessionId,
            UserId: tellerUser,
            Role: UserRole.BranchTeller,
            BranchCode: "0101",
            TerminalIp: "192.168.1.15");

        var loginRes = await api.PostAsync("/api/v1/security/sessions/login", new APIRequestContextOptions
        {
            DataObject = loginReq
        });
        loginRes.Status.Should().Be(200);
        var session = (await loginRes.JsonAsync())?.Deserialize<UserSessionResponse>(JsonOptions);
        session.Should().NotBeNull();
        session!.Token.Should().NotBeNullOrWhiteSpace();

        // 2. Onboard verified customer
        string uniqueNid = $"9{Random.Shared.Next(100000000, 999999999)}";
        string uniqueTin = $"8{Random.Shared.Next(10000000, 99999999)}123";
        var draftReq = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: "Security Authorized Customer",
            DateOfBirth: new DateOnly(1990, 5, 20),
            Gender: "Male",
            FatherName: "Father",
            MotherName: "Mother",
            StreetAddress: "123 Banking Road",
            ThanaOrUpazila: "Motijheel",
            District: "Dhaka",
            PostalCode: "1000",
            MobileNumber: "+8801700112233",
            SectorCode: "9901",
            NationalId: uniqueNid,
            TaxId: uniqueTin);

        var draftRes = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = draftReq
        });
        draftRes.Status.Should().Be(201);
        var draft = (await draftRes.JsonAsync())?.Deserialize<CustomerResponseDto>(JsonOptions)!;

        var verifyRes = await api.PostAsync($"/api/v1/customers/{draft.CifNumber}/verify", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "CHECKER01" } },
            DataObject = new { isApproved = true }
        });
        verifyRes.Status.Should().Be(200);

        // 3. Teller opens account using Bearer token
        var openAccReq = new OpenDepositAccountRequest(
            CifNumber: draft.CifNumber,
            ProductType: DepositProductType.SavingsAccount,
            BranchCode: "0101",
            InitialDeposit: 5000m,
            HasTin: true);

        var openRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {session.Token}"
            },
            DataObject = openAccReq
        });
        openRes.Status.Should().Be(201);
        var acc = (await openRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;

        // 4. Teller posts financial deposit using Bearer token
        var txReq = new PostTransactionRequest(
            AccountNumber: acc.AccountNumber,
            Type: DebitCredit.Credit,
            Amount: 10000m,
            Narration: "Authorized Teller Deposit");

        var finRes = await api.PostAsync($"/api/v1/accounts/{acc.AccountNumber}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {session.Token}"
            },
            DataObject = txReq
        });

        // 5. Teller operation succeeds with HTTP 200
        finRes.Status.Should().Be(200);
        var txBody = await finRes.TextAsync();
        txBody.Should().Contain(acc.AccountNumber);
    }

    [Fact]
    public async Task PlaywrightE2E_ConcurrentLogin_ParallelRaceCondition_BlocksSimultaneousLogins()
    {
        var api = await _fixture.CreateApiContextAsync();
        var uniqueSuffix = Random.Shared.Next(10000, 99999);
        var targetUser = $"PARALLEL_USER_{uniqueSuffix}";

        var statuses = new System.Collections.Concurrent.ConcurrentBag<int>();
        var errors = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Act: 5 simultaneous login attempts for the exact same user ID
        await Parallel.ForEachAsync(Enumerable.Range(1, 5), async (i, ct) =>
        {
            var req = new LoginSessionRequest(
                SessionId: $"SES-PAR-{uniqueSuffix}-{i}",
                UserId: targetUser,
                Role: UserRole.BranchTeller,
                BranchCode: "0101",
                TerminalIp: $"192.168.1.{i + 50}");

            var res = await api.PostAsync("/api/v1/security/sessions/login", new APIRequestContextOptions
            {
                DataObject = req
            });

            statuses.Add(res.Status);
            if (res.Status != 200)
            {
                errors.Add(await res.TextAsync());
            }
        });

        // Assert: Atomic race-condition lock guarantees exactly 1 success, remaining 4 rejected
        statuses.Count(s => s == 200).Should().Be(1);
        statuses.Count(s => s == 400).Should().Be(4);
        errors.Should().AllSatisfy(err => err.Should().Contain("BRPD 10.3.5-viii"));
    }

    [Fact]
    public async Task PlaywrightE2E_FinancialEndpoint_RejectsInvalidOrExpiredBearerToken_WithUnauthorized401()
    {
        var api = await _fixture.CreateApiContextAsync();

        var txReq = new PostTransactionRequest(
            AccountNumber: "0101101000015",
            Type: DebitCredit.Credit,
            Amount: 5000m,
            Narration: "Attempt with Fake Token");

        var res = await api.PostAsync("/api/v1/accounts/0101101000015/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid.signature"
            },
            DataObject = txReq
        });

        res.Status.Should().Be(401);
        var text = await res.TextAsync();
        text.Should().Contain("UNAUTHORIZED");
    }

    [Fact]
    public async Task PlaywrightE2E_SecurityAudit_DurableEfCorePersistence_AndAtomicSequenceIntegrity()
    {
        var api = await _fixture.CreateApiContextAsync();
        var uniqueSuffix = Random.Shared.Next(1000, 9999);
        var officerId = $"AUDITOR_{uniqueSuffix}";

        // Act 1: Record 3 consecutive audit entries
        var e1Req = new LogAuditEntryRequest(
            UserId: officerId,
            Role: UserRole.BranchTeller,
            BranchCode: "0101",
            EventCategory: AuditEventCategory.FinancialTransaction,
            Action: "CashDepositCompleted",
            TargetEntityName: "DepositAccount",
            TargetEntityId: $"0101101{uniqueSuffix:D6}",
            BeforeStateJson: "{\"Balance\":10000}",
            AfterStateJson: "{\"Balance\":25000}",
            TerminalIp: "192.168.1.101");

        var e1Res = await api.PostAsync("/api/v1/audit/log", new APIRequestContextOptions { DataObject = e1Req });
        e1Res.Status.Should().Be(200);
        var e1 = (await e1Res.JsonAsync())?.Deserialize<AuditJournalResponse>(JsonOptions)!;
        e1.IsHashValid.Should().BeTrue();

        var e2Req = new LogAuditEntryRequest(
            UserId: officerId,
            Role: UserRole.CadOfficer,
            BranchCode: "0101",
            EventCategory: AuditEventCategory.SecurityOverride,
            Action: "InterestRateOverride",
            TargetEntityName: "DepositAccount",
            TargetEntityId: $"0101101{uniqueSuffix:D6}",
            BeforeStateJson: "{\"Rate\":6.5}",
            AfterStateJson: "{\"Rate\":7.0}",
            TerminalIp: "192.168.1.102");

        var e2Res = await api.PostAsync("/api/v1/audit/log", new APIRequestContextOptions { DataObject = e2Req });
        e2Res.Status.Should().Be(200);
        var e2 = (await e2Res.JsonAsync())?.Deserialize<AuditJournalResponse>(JsonOptions)!;
        e2.IsHashValid.Should().BeTrue();
        e2.SequenceNumber.Should().Be(e1.SequenceNumber + 1);
        e2.PreviousEntryHash.Should().Be(e1.CurrentEntryHash);

        var e3Req = new LogAuditEntryRequest(
            UserId: officerId,
            Role: UserRole.ComplianceAuditor,
            BranchCode: "0101",
            EventCategory: AuditEventCategory.SystemConfiguration,
            Action: "DualControlKeyRotated",
            TargetEntityName: "VaultSecurity",
            TargetEntityId: "VAULT-01",
            BeforeStateJson: null,
            AfterStateJson: "{\"KeyId\":\"K-992\"}",
            TerminalIp: "192.168.1.103");

        var e3Res = await api.PostAsync("/api/v1/audit/log", new APIRequestContextOptions { DataObject = e3Req });
        e3Res.Status.Should().Be(200);
        var e3 = (await e3Res.JsonAsync())?.Deserialize<AuditJournalResponse>(JsonOptions)!;
        e3.IsHashValid.Should().BeTrue();
        e3.SequenceNumber.Should().Be(e2.SequenceNumber + 1);
        e3.PreviousEntryHash.Should().Be(e2.CurrentEntryHash);

        // Act 2: Verify all records are durably persisted in EF Core
        var caatsRes = await api.GetAsync("/api/v1/audit/caats-extract");
        caatsRes.Status.Should().Be(200);
        var extract = (await caatsRes.JsonAsync())?.Deserialize<CaatsAuditExtractResponse>(JsonOptions)!;
        extract.IsChainIntegrityValid.Should().BeTrue();
        extract.Entries.Should().Contain(x => x.SequenceNumber == e1.SequenceNumber);
        extract.Entries.Should().Contain(x => x.SequenceNumber == e2.SequenceNumber);
        extract.Entries.Should().Contain(x => x.SequenceNumber == e3.SequenceNumber);
    }

    [Fact]
    public async Task PlaywrightE2E_BfiuReporting_AutoAggregateCtr_AndGoAmlXmlExport()
    {
        var api = await _fixture.CreateApiContextAsync();
        var uniqueSuffix = Random.Shared.Next(1000, 9999);
        var ctrRef = $"CTR-AUTO-{uniqueSuffix}";
        var reportDate = new DateTime(2026, 8, 15);

        // Act 1: Auto aggregate CTR with candidate cash deposits
        // Customer 1: 2,500,000 BDT in 4 transactions (>= 1,000,000 BDT statutory CTR threshold)
        // Customer 2: 850,000 BDT in 2 transactions (< 1,000,000 BDT below threshold)
        var autoReq = new AutoAggregateCtrRequest(
            ReportReference: ctrRef,
            ReportDate: reportDate,
            GeneratedByUserId: "COMPLIANCE_HEAD_01",
            BranchCode: "0101",
            Aggregates: new List<CustomerCashAggregateInput>
            {
                new("CIF-AUTO-01", "Mega Corp Ltd", 4, 2500000m),
                new("CIF-AUTO-02", "Small Grocery Store", 2, 850000m)
            });

        var autoRes = await api.PostAsync("/api/v1/compliance/bfiu/ctr/auto-aggregate", new APIRequestContextOptions
        {
            DataObject = autoReq
        });
        autoRes.Status.Should().Be(200);
        var ctrReport = (await autoRes.JsonAsync())?.Deserialize<BfiuReportResponse>(JsonOptions)!;
        ctrReport.ReportReference.Should().Be(ctrRef);
        ctrReport.ReportType.Should().Be("CTR");
        ctrReport.TotalItems.Should().Be(1);
        ctrReport.CtrItems.Should().HaveCount(1);
        ctrReport.CtrItems![0].CifNumber.Should().Be("CIF-AUTO-01");
        ctrReport.CtrItems[0].TotalCashAmount.Should().Be(2500000m);

        // Act 2: Export report to UNODC / BFIU standard goAML XML
        var exportRes = await api.GetAsync($"/api/v1/compliance/bfiu/reports/{ctrRef}/goaml-export?format=xml");
        exportRes.Status.Should().Be(200);
        var exportData = (await exportRes.JsonAsync())?.Deserialize<GoAmlExportResponseDto>(JsonOptions)!;
        exportData.ReportReference.Should().Be(ctrRef);
        exportData.Format.Should().Be("xml");
        exportData.Payload.Should().Contain("<report");
        exportData.Payload.Should().Contain("<rentity_id>BFIU-BANK-001</rentity_id>");
        exportData.Payload.Should().Contain("<report_code>CTR</report_code>");
        exportData.Payload.Should().Contain("<party_cif>CIF-AUTO-01</party_cif>");
        exportData.Payload.Should().Contain("2500000");

        // Act 3: Verify report status updated to SubmittedToGoAml
        var listRes = await api.GetAsync("/api/v1/compliance/bfiu/reports");
        listRes.Status.Should().Be(200);
        var allReports = (await listRes.JsonAsync())?.Deserialize<List<BfiuReportResponse>>(JsonOptions)!;
        var persistedReport = allReports.FirstOrDefault(r => r.ReportReference == ctrRef);
        persistedReport.Should().NotBeNull();
        persistedReport!.Status.Should().Be("SubmittedToGoAml");
        persistedReport.GoAmlFormat.Should().Be("xml");
    }

    [Fact]
    public async Task PlaywrightE2E_CaatsIntegrity_DetectsTamperingAndPinpointsSequence()
    {
        var api = await _fixture.CreateApiContextAsync();

        // Act 1: Verify current clean audit chain
        var verifyRes = await api.GetAsync("/api/v1/audit/caats/verify-integrity");
        verifyRes.Status.Should().Be(200);
        var verifyResult = (await verifyRes.JsonAsync())?.Deserialize<CaatsIntegrityVerificationResultDto>(JsonOptions)!;

        // Chain must be completely valid
        verifyResult.IsChainIntegrityValid.Should().BeTrue();
        verifyResult.TamperDetectedAtSequence.Should().BeNull();
        verifyResult.Remarks.Should().Contain("Audit trail cryptographic integrity verified 100%");
    }
}
