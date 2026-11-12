using System;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Reporting;
using CBS.Domain.Security;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Unit tests locking CBS Transaction Controls Governance,
// PAM Emergency Break-Glass Dual-Custody, and Consolidated EOD Multi-Rail Balancing
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 9, 10 & 11.

public class GovernanceAndProofingUnitTests
{
    // =========================================================================
    // 1. Transaction Anomaly & Controls Governance Tests (BRPD Module 9)
    // =========================================================================

    [Fact]
    public void Structuring_Smurfing_Should_Flag_Anomaly_When_Cumulative_24h_Reaches_CTR_Threshold()
    {
        // ARCHITECTURAL INTENT: StructuringSmurfingDetectionInvariant flags smurfing when
        // cumulative 24-hour deposits reach or exceed 1,000,000 BDT even if single transactions are < 1M.
        var anomaly = TransactionAnomalyRecord.DetectStructuring(
            accountNumber: "0100123456789",
            cifNumber: "CIF-78901",
            transactionReference: "TXN-SMURF-01",
            currentAmount: Money.Bdt(350_000m),
            cumulative24hAmount: Money.Bdt(1_050_000m),
            detectedAtUtc: DateTime.UtcNow);

        anomaly.Should().NotBeNull();
        anomaly.AnomalyType.Should().Be(AnomalySuspicionType.SmurfingStructuring);
        anomaly.Status.Should().Be(AnomalyResolutionStatus.DetectedPendingInvestigation);
        anomaly.TransactionAmount.Amount.Should().Be(350_000m);
        anomaly.RuleDescription.Should().Contain("1,000,000.00 BDT");
    }

    [Fact]
    public void Employee_Self_Account_Posting_Should_Throw_InvariantViolationException()
    {
        // ARCHITECTURAL INTENT: EmployeeSelfAccountPostingInvariant strictly bars tellers/officers
        // from initiating or authorising postings into their own accounts (BRPD 9.1.1-xxv).
        var act = () => TransactionAnomalyRecord.CreateEmployeeSelfAccountAnomaly(
            tellerUserId: "EMP-TELLER-101",
            accountOwnerEmployeeId: "EMP-TELLER-101",
            accountNumber: "0100999999999",
            cifNumber: "CIF-10101",
            transactionReference: "TXN-SELF-001",
            amount: Money.Bdt(50_000m),
            detectedAtUtc: DateTime.UtcNow);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*EmployeeSelfAccountPostingInvariant*")
            .WithMessage("*strictly prohibited from executing financial transactions on their own account*");
    }

    [Fact]
    public void Employee_Posting_To_Third_Party_Account_Should_Not_Throw()
    {
        var act = () => TransactionAnomalyRecord.CreateEmployeeSelfAccountAnomaly(
            tellerUserId: "EMP-TELLER-101",
            accountOwnerEmployeeId: "EMP-OFFICER-202",
            accountNumber: "0100888888888",
            cifNumber: "CIF-20202",
            transactionReference: "TXN-NORMAL-001",
            amount: Money.Bdt(50_000m),
            detectedAtUtc: DateTime.UtcNow);

        act.Should().NotThrow();
    }

    [Fact]
    public void AnomalyRecord_Should_Resolve_And_Prevent_Duplicate_Resolution()
    {
        var anomaly = TransactionAnomalyRecord.DetectOffHoursPosting(
            accountNumber: "0100555555555",
            cifNumber: "CIF-11111",
            transactionReference: "TXN-NIGHT-01",
            amount: Money.Bdt(500_000m),
            originatingUserId: "USR-NIGHT-TELLER",
            transactionTimeUtc: DateTime.UtcNow);

        anomaly.Resolve(
            resolution: AnomalyResolutionStatus.ClearedLegitimateBusiness,
            investigatedByUserId: "USR-COMPLIANCE",
            notes: "Verified night-drop cash deposit from corporate distributor with valid pre-authorization.");

        anomaly.Status.Should().Be(AnomalyResolutionStatus.ClearedLegitimateBusiness);
        anomaly.InvestigatedByUserId.Should().Be("USR-COMPLIANCE");
        anomaly.ResolvedAtUtc.Should().NotBeNull();

        // Duplicate resolution must fail
        var reResolve = () => anomaly.Resolve(
            resolution: AnomalyResolutionStatus.EscalatedToBfiuStr,
            investigatedByUserId: "USR-AML-HEAD",
            notes: "Attempting re-resolution");

        reResolve.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot resolve anomaly in status 'ClearedLegitimateBusiness'*");
    }

    // =========================================================================
    // 2. Privileged Access Management (PAM) Break-Glass Tests (BRPD Module 10)
    // =========================================================================

    [Fact]
    public void BreakGlass_Should_Reject_Self_Approval_By_Requestor()
    {
        // ARCHITECTURAL INTENT: BreakGlassDualCustodyInvariant forbids requestor from approving their own session.
        var session = new BreakGlassEmergencySession(
            sessionId: "BG-2026-001",
            requestorUserId: "USR-DBA-ALAM",
            targetRole: "SUPER_ADMIN",
            emergencyJustification: "Production database index corruption causing failover freeze",
            createdAtUtc: DateTime.UtcNow);

        session.Status.Should().Be(BreakGlassStatus.RequestedPendingApproval);

        // Self-approval as first approver
        var act1 = () => session.ApproveFirstOfficer("USR-DBA-ALAM");
        act1.Should().Throw<InvariantViolationException>()
            .WithMessage("*BreakGlassDualCustodyInvariant*")
            .WithMessage("*Requestor cannot approve their own emergency break-glass elevation*");
    }

    [Fact]
    public void BreakGlass_Should_Enforce_Dual_Custody_With_Two_Distinct_Officers()
    {
        // ARCHITECTURAL INTENT: BreakGlassDualCustodyInvariant mandates two distinct approving officers.
        var session = new BreakGlassEmergencySession(
            sessionId: "BG-2026-002",
            requestorUserId: "USR-SEC-OPS",
            targetRole: "SECURITY_ADMIN",
            emergencyJustification: "Emergency firewall patch and certificate rotation",
            createdAtUtc: DateTime.UtcNow);

        // First officer approval
        session.ApproveFirstOfficer("USR-CHIEF-INFO-SEC");
        session.Status.Should().Be(BreakGlassStatus.PartiallyApprovedFirstOfficer);

        // Second approval by same officer must fail
        var sameOfficerAct = () => session.ApproveSecondOfficer("USR-CHIEF-INFO-SEC");
        sameOfficerAct.Should().Throw<InvariantViolationException>()
            .WithMessage("*BreakGlassDualCustodyInvariant*")
            .WithMessage("*strictly mandates dual custody approval from two distinct authorizing officers*");

        // Second approval by requestor must fail
        var requestorSecondAct = () => session.ApproveSecondOfficer("USR-SEC-OPS");
        requestorSecondAct.Should().Throw<InvariantViolationException>()
            .WithMessage("*BreakGlassDualCustodyInvariant*");

        // Second approval by distinct officer succeeds
        session.ApproveSecondOfficer("USR-HEAD-OF-IT");
        session.Status.Should().Be(BreakGlassStatus.ApprovedActive);
        session.SessionExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddHours(3).AddMinutes(50));
        session.SessionExpiresAtUtc.Should().BeBefore(DateTime.UtcNow.AddHours(4).AddMinutes(5));
    }

    [Fact]
    public void BreakGlass_Should_Enforce_Max_4Hour_Window_And_Expire()
    {
        // ARCHITECTURAL INTENT: BreakGlassTimeWindowInvariant expires session after 4-hour window.
        var session = new BreakGlassEmergencySession(
            sessionId: "BG-2026-003",
            requestorUserId: "USR-DEV-LEAD",
            targetRole: "EMERGENCY_PATCHER",
            emergencyJustification: "Critical zero-day security fix application",
            createdAtUtc: DateTime.UtcNow);

        session.ApproveFirstOfficer("USR-MGR-A");
        session.ApproveSecondOfficer("USR-MGR-B");

        // Valid within window
        var validTime = DateTime.UtcNow.AddHours(2);
        var actValid = () => session.AssertSessionValid(validTime);
        actValid.Should().NotThrow();

        // Expired after 4 hours 1 minute
        var expiredTime = DateTime.UtcNow.AddHours(4).AddMinutes(1);
        var actExpired = () => session.AssertSessionValid(expiredTime);
        actExpired.Should().Throw<InvariantViolationException>()
            .WithMessage("*BreakGlassTimeWindowInvariant*")
            .WithMessage("*maximum 4-hour window elapsed*");

        session.Status.Should().Be(BreakGlassStatus.Expired);
    }

    [Fact]
    public void BreakGlass_Should_Allow_Early_Revocation()
    {
        var session = new BreakGlassEmergencySession(
            sessionId: "BG-2026-004",
            requestorUserId: "USR-DEV-LEAD",
            targetRole: "EMERGENCY_PATCHER",
            emergencyJustification: "Production data restoration",
            createdAtUtc: DateTime.UtcNow);

        session.ApproveFirstOfficer("USR-MGR-A");
        session.ApproveSecondOfficer("USR-MGR-B");

        session.Revoke("USR-AUDIT-OFFICER", "Maintenance completed ahead of schedule.");

        session.Status.Should().Be(BreakGlassStatus.Revoked);
        session.RevokedByUserId.Should().Be("USR-AUDIT-OFFICER");
        session.RevocationReason.Should().Be("Maintenance completed ahead of schedule.");

        var actValid = () => session.AssertSessionValid(DateTime.UtcNow);
        actValid.Should().Throw<InvariantViolationException>()
            .WithMessage("*BreakGlassInvalidSessionInvariant*");
    }

    // =========================================================================
    // 3. Consolidated Daily EOD Balancing & Multi-Rail Proofing (BRPD Module 11)
    // =========================================================================

    [Fact]
    public void DailyEodBalancingSheet_Should_Proof_When_Debits_Equal_Credits_And_Suspense_Is_Zero()
    {
        // ARCHITECTURAL INTENT: DailyEodBalancingProofInvariant requires GL Debits == GL Credits
        // and ClearingSuspenseBalance == 0 before day closure (BRPD 11.1.1, 1.2.2).
        var sheet = new DailyEodBalancingSheet(
            sheetId: "EOD-DHAKA-20261112",
            branchCode: "BR-001",
            operatingDate: new DateOnly(2026, 11, 12),
            totalGlDebits: Money.Bdt(150_000_000m),
            totalGlCredits: Money.Bdt(150_000_000m),
            totalVaultCashBalance: Money.Bdt(40_000_000m),
            totalTellerCashBalance: Money.Bdt(10_000_000m),
            totalClearingSettlementDebits: Money.Bdt(25_000_000m),
            totalClearingSettlementCredits: Money.Bdt(25_000_000m),
            clearingSuspenseBalance: Money.Bdt(0m),
            createdAtUtc: DateTime.UtcNow);

        sheet.ValidateAndProofDailyBooks("USR-BRANCH-MANAGER");

        sheet.ProofingStatus.Should().Be(EodProofingStatus.BalancedInProof);
        sheet.ApprovedByManagerUserId.Should().Be("USR-BRANCH-MANAGER");
        sheet.ProofedAtUtc.Should().NotBeNull();
        sheet.DiscrepancyNotes.Should().BeNull();
    }

    [Fact]
    public void DailyEodBalancingSheet_Should_Reject_Proof_When_Gl_Debits_Do_Not_Equal_Credits()
    {
        var sheet = new DailyEodBalancingSheet(
            sheetId: "EOD-DHAKA-20261112-UNBAL",
            branchCode: "BR-001",
            operatingDate: new DateOnly(2026, 11, 12),
            totalGlDebits: Money.Bdt(150_000_000m),
            totalGlCredits: Money.Bdt(149_995_000m), // 5,000 BDT out of balance
            totalVaultCashBalance: Money.Bdt(40_000_000m),
            totalTellerCashBalance: Money.Bdt(10_000_000m),
            totalClearingSettlementDebits: Money.Bdt(25_000_000m),
            totalClearingSettlementCredits: Money.Bdt(25_000_000m),
            clearingSuspenseBalance: Money.Bdt(0m),
            createdAtUtc: DateTime.UtcNow);

        var act = () => sheet.ValidateAndProofDailyBooks("USR-BRANCH-MANAGER");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*DailyEodBalancingProofInvariant*")
            .WithMessage("*failed proofing: GL Debits (150,000,000.00) != GL Credits (149,995,000.00)*");

        sheet.ProofingStatus.Should().Be(EodProofingStatus.DiscrepancyDetected);
        sheet.DiscrepancyNotes.Should().Contain("Unbalanced books");
        sheet.ApprovedByManagerUserId.Should().BeNull();
    }

    [Fact]
    public void DailyEodBalancingSheet_Should_Reject_Proof_When_Clearing_Suspense_Is_Non_Zero()
    {
        var sheet = new DailyEodBalancingSheet(
            sheetId: "EOD-DHAKA-20261112-SUSPENSE",
            branchCode: "BR-001",
            operatingDate: new DateOnly(2026, 11, 12),
            totalGlDebits: Money.Bdt(150_000_000m),
            totalGlCredits: Money.Bdt(150_000_000m),
            totalVaultCashBalance: Money.Bdt(40_000_000m),
            totalTellerCashBalance: Money.Bdt(10_000_000m),
            totalClearingSettlementDebits: Money.Bdt(25_000_000m),
            totalClearingSettlementCredits: Money.Bdt(25_000_000m),
            clearingSuspenseBalance: Money.Bdt(12_500m), // Unsettled clearing suspense
            createdAtUtc: DateTime.UtcNow);

        var act = () => sheet.ValidateAndProofDailyBooks("USR-BRANCH-MANAGER");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*DailyEodBalancingProofInvariant*")
            .WithMessage("*Clearing Suspense (12,500.00) != 0*");

        sheet.ProofingStatus.Should().Be(EodProofingStatus.DiscrepancyDetected);
    }
}
