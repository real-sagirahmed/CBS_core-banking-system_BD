using System;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Security;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Unit tests locking Delegation of Authority (DOA) Matrix,
// Temporary Authority Absence Delegation, and Enterprise Password Lifecycle Invariants
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10 (System Administration).

public class EnterpriseDoaAndSecurityUnitTests
{
    // =========================================================================
    // 1. Delegation of Authority (DOA) Limit Matrix Tests (BRPD 10.1.5-d & 10.2.3)
    // =========================================================================

    [Fact]
    public void DoaProfile_Should_Allow_Transaction_Within_Approved_Limit()
    {
        var profile = new UserDoaProfile(
            profileId: "DOA-TELLER-01",
            userId: "USR-TELLER-01",
            roleName: "TELLER",
            branchCode: "BR-001",
            maxLocalDebitLimit: Money.Bdt(500_000m),
            maxLocalCreditLimit: Money.Bdt(1_000_000m),
            maxInterBranchDebitLimit: Money.Bdt(200_000m),
            maxInterBranchCreditLimit: Money.Bdt(500_000m),
            maxGlAdjustmentLimit: Money.Bdt(50_000m),
            assignedByUserId: "ADMIN-01",
            assignedAtUtc: DateTime.UtcNow);

        // Within limit (350,000 <= 500,000)
        var actDebit = () => profile.AssertTransactionAllowed(TransactionLimitType.LocalDebit, Money.Bdt(350_000m));
        actDebit.Should().NotThrow();

        // Exactly at limit (500,000 <= 500,000)
        var actExact = () => profile.AssertTransactionAllowed(TransactionLimitType.LocalDebit, Money.Bdt(500_000m));
        actExact.Should().NotThrow();
    }

    [Fact]
    public void DoaProfile_Should_Reject_Transaction_Exceeding_Approved_Limit()
    {
        // ARCHITECTURAL INTENT: TransactionDoaCeilingInvariant prevents single-teller posting above DOA limit.
        var profile = new UserDoaProfile(
            profileId: "DOA-TELLER-02",
            userId: "USR-TELLER-02",
            roleName: "TELLER",
            branchCode: "BR-001",
            maxLocalDebitLimit: Money.Bdt(500_000m),
            maxLocalCreditLimit: Money.Bdt(1_000_000m),
            maxInterBranchDebitLimit: Money.Bdt(200_000m),
            maxInterBranchCreditLimit: Money.Bdt(500_000m),
            maxGlAdjustmentLimit: Money.Bdt(50_000m),
            assignedByUserId: "ADMIN-01",
            assignedAtUtc: DateTime.UtcNow);

        // Exceeds debit limit (500,000.01 > 500,000)
        var actOverDebit = () => profile.AssertTransactionAllowed(TransactionLimitType.LocalDebit, Money.Bdt(500_000.01m));
        actOverDebit.Should().Throw<InvariantViolationException>()
            .WithMessage("*TransactionDoaCeilingInvariant*")
            .WithMessage("*Higher supervisor dual-authorization required*");

        // Exceeds GL adjustment limit (75,000 > 50,000)
        var actOverGl = () => profile.AssertTransactionAllowed(TransactionLimitType.GlManualAdjustment, Money.Bdt(75_000m));
        actOverGl.Should().Throw<InvariantViolationException>()
            .WithMessage("*TransactionDoaCeilingInvariant*");
    }

    // =========================================================================
    // 2. Temporary Authority & Absence Delegation Tests (BRPD 10.2.4 & 10.2.5)
    // =========================================================================

    [Fact]
    public void TemporaryDelegation_Should_Reject_Self_Delegation()
    {
        var now = DateTime.UtcNow;
        var act = () => new TemporaryAuthorityDelegation(
            delegationId: "DEL-001",
            delegatorUserId: "MGR-ALAM",
            delegateeUserId: "MGR-ALAM", // Self delegation
            delegatedRole: "BRANCH_MANAGER",
            branchCode: "BR-001",
            effectiveFromUtc: now,
            effectiveToUtc: now.AddDays(7),
            delegationReason: "Annual leave",
            createdAtUtc: now);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*SelfDelegationForbiddenInvariant*")
            .WithMessage("*cannot delegate temporary authority to themselves*");
    }

    [Fact]
    public void TemporaryDelegation_Should_Enforce_Dual_Custody_Approval()
    {
        var now = DateTime.UtcNow;
        var delegation = new TemporaryAuthorityDelegation(
            delegationId: "DEL-002",
            delegatorUserId: "MGR-ALAM",
            delegateeUserId: "OFFICER-HASAN",
            delegatedRole: "ACTING_BRANCH_MANAGER",
            branchCode: "BR-001",
            effectiveFromUtc: now,
            effectiveToUtc: now.AddDays(5),
            delegationReason: "Medical leave",
            createdAtUtc: now);

        delegation.Status.Should().Be(DelegationStatus.PendingApproval);

        // Delegator cannot approve own delegation
        var actDelegatorApprove = () => delegation.Approve("MGR-ALAM");
        actDelegatorApprove.Should().Throw<InvariantViolationException>()
            .WithMessage("*DelegationDualCustodyInvariant*");

        // Delegatee cannot approve own delegation
        var actDelegateeApprove = () => delegation.Approve("OFFICER-HASAN");
        actDelegateeApprove.Should().Throw<InvariantViolationException>()
            .WithMessage("*DelegationDualCustodyInvariant*");

        // Independent authorizing officer approves
        delegation.Approve("ZONAL-HEAD-RAHMAN");
        delegation.Status.Should().Be(DelegationStatus.Active);
        delegation.ApprovedByUserId.Should().Be("ZONAL-HEAD-RAHMAN");
    }

    [Fact]
    public void TemporaryDelegation_Should_Enforce_Effective_Time_Window_And_Expire()
    {
        var now = DateTime.UtcNow;
        var delegation = new TemporaryAuthorityDelegation(
            delegationId: "DEL-003",
            delegatorUserId: "MGR-ALAM",
            delegateeUserId: "OFFICER-HASAN",
            delegatedRole: "ACTING_BRANCH_MANAGER",
            branchCode: "BR-001",
            effectiveFromUtc: now.AddHours(1),
            effectiveToUtc: now.AddDays(3),
            delegationReason: "Official training",
            createdAtUtc: now);

        delegation.Approve("ZONAL-HEAD-01");

        // Before start time -> PrematureDelegationInvariant
        var actPremature = () => delegation.AssertIsActive(now);
        actPremature.Should().Throw<InvariantViolationException>()
            .WithMessage("*PrematureDelegationInvariant*");

        // Within active window -> Valid
        var actValid = () => delegation.AssertIsActive(now.AddDays(1));
        actValid.Should().NotThrow();

        // After end time -> ExpiredDelegationInvariant
        var actExpired = () => delegation.AssertIsActive(now.AddDays(4));
        actExpired.Should().Throw<InvariantViolationException>()
            .WithMessage("*ExpiredDelegationInvariant*");

        delegation.Status.Should().Be(DelegationStatus.Expired);
    }

    // =========================================================================
    // 3. Password Lifecycle, History Lock & Expiry Tests (BRPD 10.1.3)
    // =========================================================================

    [Fact]
    public void UserPassword_Should_Enforce_First_Login_Change()
    {
        // ARCHITECTURAL INTENT: FirstLoginPasswordChangeRequiredInvariant enforces default password reset (BRPD 10.1.3-iv).
        var credential = new UserPasswordCredential(
            userId: "NEW-OFFICER-01",
            passwordHash: "HASH_DEFAULT_123",
            salt: "SALT_01",
            isFirstLoginPendingChange: true,
            createdAtUtc: DateTime.UtcNow);

        var act = () => credential.AssertPasswordNotExpired(DateTime.UtcNow);
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*FirstLoginPasswordChangeRequiredInvariant*")
            .WithMessage("*must change password immediately*");
    }

    [Fact]
    public void UserPassword_Should_Enforce_Four_Times_History_Lock()
    {
        // ARCHITECTURAL INTENT: PasswordHistoryFourTimesInvariant prevents reuse of last 4 passwords (BRPD 10.1.3-v).
        var now = DateTime.UtcNow;
        var credential = new UserPasswordCredential(
            userId: "USR-SECURITY-01",
            passwordHash: "HASH_PASS_1",
            salt: "SALT_1",
            isFirstLoginPendingChange: false,
            createdAtUtc: now);

        // Change 1: HASH_PASS_2 (History: HASH_PASS_1)
        credential.ChangePassword("HASH_PASS_2", "SALT_2", now.AddDays(1));

        // Change 2: HASH_PASS_3 (History: HASH_PASS_1, HASH_PASS_2)
        credential.ChangePassword("HASH_PASS_3", "SALT_3", now.AddDays(2));

        // Change 3: HASH_PASS_4 (History: HASH_PASS_1, HASH_PASS_2, HASH_PASS_3)
        credential.ChangePassword("HASH_PASS_4", "SALT_4", now.AddDays(3));

        // Attempting to reuse HASH_PASS_1 must fail
        var actReusePass1 = () => credential.ChangePassword("HASH_PASS_1", "SALT_X", now.AddDays(4));
        actReusePass1.Should().Throw<InvariantViolationException>()
            .WithMessage("*PasswordHistoryFourTimesInvariant*")
            .WithMessage("*cannot match any of the previous 4 passwords*");

        // Attempting to reuse current password HASH_PASS_4 must fail
        var actReuseCurrent = () => credential.ChangePassword("HASH_PASS_4", "SALT_X", now.AddDays(4));
        actReuseCurrent.Should().Throw<InvariantViolationException>()
            .WithMessage("*PasswordHistoryFourTimesInvariant*");

        // Change 4: HASH_PASS_5 (Valid, history has 4 items: HASH_PASS_1..4)
        credential.ChangePassword("HASH_PASS_5", "SALT_5", now.AddDays(5));
        credential.PasswordHistory.Should().HaveCount(4);

        // Change 5: HASH_PASS_6 (Valid, pushes HASH_PASS_1 out of 4-item history)
        credential.ChangePassword("HASH_PASS_6", "SALT_6", now.AddDays(6));
        credential.PasswordHistory.Should().HaveCount(4);
        credential.PasswordHistory.Should().NotContain("HASH_PASS_1");

        // Now HASH_PASS_1 can be safely set again as it's no longer in the 4-item history
        credential.ChangePassword("HASH_PASS_1", "SALT_NEW", now.AddDays(7));
        credential.PasswordHash.Should().Be("HASH_PASS_1");
    }

    [Fact]
    public void UserPassword_Should_Enforce_30_Day_Expiration()
    {
        // ARCHITECTURAL INTENT: PasswordExpiryThirtyDaysInvariant enforces mandatory 30-day reset (BRPD 10.1.3-vi).
        var pastDate = DateTime.UtcNow.AddDays(-31);
        var credential = new UserPasswordCredential(
            userId: "USR-OLD-PASS",
            passwordHash: "HASH_MONTH_AGO",
            salt: "SALT_1",
            isFirstLoginPendingChange: false,
            createdAtUtc: pastDate);

        var act = () => credential.AssertPasswordNotExpired(DateTime.UtcNow);
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*PasswordExpiryThirtyDaysInvariant*")
            .WithMessage("*expired*");
    }

    [Fact]
    public void UserPassword_Should_Lockout_After_Five_Failed_Attempts()
    {
        var now = DateTime.UtcNow;
        var credential = new UserPasswordCredential(
            userId: "USR-BRUTE-FORCE",
            passwordHash: "HASH_SECRET",
            salt: "SALT_1",
            isFirstLoginPendingChange: false,
            createdAtUtc: now);

        credential.IsLockedOut.Should().BeFalse();

        for (int i = 0; i < 4; i++)
        {
            credential.RecordFailedAttempt(now);
            credential.IsLockedOut.Should().BeFalse();
        }

        // 5th failed attempt triggers account lockout
        credential.RecordFailedAttempt(now);
        credential.IsLockedOut.Should().BeTrue();
        credential.LockoutExpiryUtc.Should().BeAfter(now.AddMinutes(29));
    }
}
