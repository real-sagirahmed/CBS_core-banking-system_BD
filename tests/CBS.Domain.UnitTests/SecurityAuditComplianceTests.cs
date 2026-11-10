using CBS.Domain.Enums;
using CBS.Domain.SecurityAudit;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class SecurityAuditComplianceTests
{
    [Fact]
    public void UserSession_WhenConcurrentActiveSessionExists_ShouldThrowMultipleSessionViolation()
    {
        // Act: Attempt to start new session when an active session already exists for this user
        var act = () => UserSession.StartSession(
            sessionId: "SESS-2026-9901",
            userId: "TELLER_01",
            role: UserRole.BranchTeller,
            branchCode: "001",
            terminalIp: "192.168.1.100",
            hasExistingActiveSession: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Multiple/concurrent login is blocked*");
    }

    [Fact]
    public void UserSession_WhenSuperAdminAttemptsFinancialTransaction_ShouldThrowAdminBlockedViolation()
    {
        // Arrange
        var adminSession = UserSession.StartSession(
            sessionId: "SESS-ADMIN-01",
            userId: "SUPER_ADMIN_01",
            role: UserRole.SystemAdmin,
            branchCode: "HO",
            terminalIp: "10.0.0.1",
            hasExistingActiveSession: false);

        // Act & Assert (BRPD 10.3.5-vii: Super/Admin prohibited from financial transactions)
        var act = () => adminSession.AssertCanPerformFinancialTransaction();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Super/Admin*shall not be authorized to perform*");
    }

    [Fact]
    public void UserSession_WhenComplianceAuditorAttemptsFinancialTransaction_ShouldThrowAuditorBlockedViolation()
    {
        var auditorSession = UserSession.StartSession(
            sessionId: "SESS-AUDIT-01",
            userId: "AUDITOR_01",
            role: UserRole.ComplianceAuditor,
            branchCode: "HO",
            terminalIp: "10.0.0.5",
            hasExistingActiveSession: false);

        var act = () => auditorSession.AssertCanPerformFinancialTransaction();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Role 'ComplianceAuditor'*is blocked from financial transactions*");
    }

    [Fact]
    public void UserSession_WhenBranchTellerAttemptsFinancialTransaction_ShouldSucceed()
    {
        // Arrange
        var tellerSession = UserSession.StartSession(
            sessionId: "SESS-TELLER-01",
            userId: "TELLER_01",
            role: UserRole.BranchTeller,
            branchCode: "001",
            terminalIp: "192.168.1.101",
            hasExistingActiveSession: false);

        // Act & Assert
        var act = () => tellerSession.AssertCanPerformFinancialTransaction();
        act.Should().NotThrow();
    }

    [Fact]
    public void AuditJournalEntry_CryptographicHashChain_ConsecutiveEntriesShouldBeLinkedAndValid()
    {
        // Entry 1 (Genesis Hash)
        var entry1 = AuditJournalEntry.Create(
            sequenceNumber: 1,
            userId: "MAKER01",
            role: UserRole.BranchTeller,
            branchCode: "001",
            eventCategory: AuditEventCategory.FinancialTransaction,
            action: "CASH_DEPOSIT",
            targetEntityName: "DepositAccount",
            targetEntityId: "0011200000018",
            beforeStateJson: "{\"balance\":5000}",
            afterStateJson: "{\"balance\":15000}",
            terminalIp: "192.168.1.105",
            previousEntryHash: AuditJournalEntry.GenesisHash);

        entry1.VerifyHash().Should().BeTrue();
        entry1.PreviousEntryHash.Should().Be(AuditJournalEntry.GenesisHash);

        // Entry 2 (Linked to Entry 1 Hash)
        var entry2 = AuditJournalEntry.Create(
            sequenceNumber: 2,
            userId: "SUPERVISOR01",
            role: UserRole.CadOfficer,
            branchCode: "001",
            eventCategory: AuditEventCategory.SecurityOverride,
            action: "OVERRIDE_POSTING_LIMIT",
            targetEntityName: "DepositAccount",
            targetEntityId: "0011200000018",
            beforeStateJson: null,
            afterStateJson: "{\"approved\":true}",
            terminalIp: "192.168.1.200",
            previousEntryHash: entry1.CurrentEntryHash);

        entry2.VerifyHash().Should().BeTrue();
        entry2.PreviousEntryHash.Should().Be(entry1.CurrentEntryHash);
    }

    [Fact]
    public void BfiuRegulatoryReport_GenerateCtr_OnlyIncludesCustomersWithAggregateCashAtLeastOneMillion()
    {
        // Arrange: 3 customers
        // Customer A: 1,500,000 BDT (Eligible >= 1,000,000 BDT)
        // Customer B: 950,000 BDT (Not eligible)
        // Customer C: 1,000,000 BDT (Eligible >= 1,000,000 BDT)
        var aggregates = new List<(string Cif, string Name, int TxCount, decimal TotalCash)>
        {
            ("1001", "Abdul Karim", 2, 1500000m),
            ("1002", "Rahim Uddin", 3, 950000m),
            ("1003", "Apex Tannery Ltd", 1, 1000000m)
        };

        // Act
        var ctr = BfiuRegulatoryReport.GenerateCtr(
            reportReference: "CTR-2026-001-001",
            reportDate: new DateTime(2026, 7, 10),
            customerCashAggregates: aggregates,
            generatedByUserId: "COMPLIANCE_OFFICER_01");

        // Assert
        ctr.ReportType.Should().Be("CTR");
        ctr.CtrItems.Should().HaveCount(2);
        ctr.CtrItems.Select(x => x.CifNumber).Should().BeEquivalentTo(new[] { "1001", "1003" });
        ctr.CtrItems.First(x => x.CifNumber == "1001").TotalCashAmount.Should().Be(1500000m);
    }

    [Fact]
    public void BfiuRegulatoryReport_GenerateStr_IncludesStructuringAndTpBreachAlerts()
    {
        // Arrange: 2 suspicious transaction alerts
        var alerts = new List<StrAlertItem>
        {
            new("1005", "Smurfing Trader", StrAlertReason.StructuringSmurfing, 980000m, "BDT", "Multiple cash deposits just below 1,000,000 BDT CTR threshold within 24 hours.", DateTime.UtcNow),
            new("1009", "Salaried Individual", StrAlertReason.TransactionProfileExceeded, 5000000m, "BDT", "Sudden 5M BDT credit exceeding monthly TP limit of 200,000 BDT.", DateTime.UtcNow)
        };

        // Act
        var str = BfiuRegulatoryReport.GenerateStr(
            reportReference: "STR-2026-001-001",
            reportDate: new DateTime(2026, 7, 10),
            alerts: alerts,
            generatedByUserId: "AML_ANALYST_01");

        // Assert
        str.ReportType.Should().Be("STR");
        str.StrAlerts.Should().HaveCount(2);
        str.StrAlerts.First().Reason.Should().Be(StrAlertReason.StructuringSmurfing);
        str.Status.Should().Be(ComplianceReportStatus.Generated);

        str.MarkSubmittedToGoAml();
        str.Status.Should().Be(ComplianceReportStatus.SubmittedToGoAml);
    }

    [Fact]
    public void AuditJournalEntry_WhenHashOrPayloadTampered_VerifyHashReturnsFalse()
    {
        var entry = AuditJournalEntry.Create(
            sequenceNumber: 1,
            userId: "MAKER01",
            role: UserRole.BranchTeller,
            branchCode: "001",
            eventCategory: AuditEventCategory.FinancialTransaction,
            action: "CASH_DEPOSIT",
            targetEntityName: "DepositAccount",
            targetEntityId: "0011200000018",
            beforeStateJson: "{\"balance\":5000}",
            afterStateJson: "{\"balance\":15000}",
            terminalIp: "192.168.1.105",
            previousEntryHash: AuditJournalEntry.GenesisHash);

        entry.VerifyHash().Should().BeTrue();

        // Tamper with state JSON
        var tampered = entry with { BeforeStateJson = "{\"balance\":9999999}" };
        tampered.VerifyHash().Should().BeFalse("Modifying internal state payload must break cryptographic hash verification");
    }

    [Fact]
    public void GoAmlXmlBuilder_BuildXml_GeneratesWellFormedXmlWithCorrectTransactions()
    {
        var aggregates = new List<(string Cif, string Name, int TxCount, decimal TotalCash)>
        {
            ("1001", "Abdul Karim", 2, 1500000m),
            ("1003", "Apex Tannery Ltd", 1, 2000000m)
        };

        var ctr = BfiuRegulatoryReport.GenerateCtr(
            reportReference: "CTR-2026-001-002",
            reportDate: new DateTime(2026, 7, 10),
            customerCashAggregates: aggregates,
            generatedByUserId: "COMPLIANCE_01",
            branchCode: "001");

        string xml = GoAmlXmlBuilder.BuildXml(ctr);

        xml.Should().Contain("<report xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
        xml.Should().Contain("<rentity_id>BFIU-BANK-001</rentity_id>");
        xml.Should().Contain("<report_code>CTR</report_code>");
        xml.Should().Contain("<party_cif>1001</party_cif>");
        xml.Should().Contain("<amount_local>1500000.00</amount_local>");
        xml.Should().Contain("<party_cif>1003</party_cif>");
        xml.Should().Contain("<amount_local>2000000.00</amount_local>");
    }
}
