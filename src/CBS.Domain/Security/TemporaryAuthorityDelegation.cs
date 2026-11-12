using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Security;

// ARCHITECTURAL INTENT: Temporary Acting Authority & Officer Absence Delegation Engine.
// Governed strictly by Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10.2.4 & 10.2.5.
// Enforces mandatory TemporaryDelegationWindowInvariant ensuring delegated powers are time-bounded,
// approved by an independent authorizing officer (dual custody), and automatically expired.

public sealed class TemporaryAuthorityDelegation : AggregateRoot<string>
{
    public string DelegationId => Id;
    public string DelegatorUserId { get; private set; } = null!;
    public string DelegateeUserId { get; private set; } = null!;
    public string DelegatedRole { get; private set; } = null!;
    public string BranchCode { get; private set; } = null!;
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime EffectiveToUtc { get; private set; }
    public string DelegationReason { get; private set; } = null!;
    public DelegationStatus Status { get; private set; }
    public string? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public string? RevokedByUserId { get; private set; }
    public string? RevocationReason { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private TemporaryAuthorityDelegation() : base(string.Empty) { }
    #pragma warning restore CS8618

    public TemporaryAuthorityDelegation(
        string delegationId,
        string delegatorUserId,
        string delegateeUserId,
        string delegatedRole,
        string branchCode,
        DateTime effectiveFromUtc,
        DateTime effectiveToUtc,
        string delegationReason,
        DateTime createdAtUtc) : base(delegationId.Trim().ToUpperInvariant())
    {
        if (string.IsNullOrWhiteSpace(delegationId))
            throw new ArgumentException("Delegation ID is required.", nameof(delegationId));
        if (string.IsNullOrWhiteSpace(delegatorUserId))
            throw new ArgumentException("Delegator user ID is required.", nameof(delegatorUserId));
        if (string.IsNullOrWhiteSpace(delegateeUserId))
            throw new ArgumentException("Delegatee user ID is required.", nameof(delegateeUserId));
        if (string.IsNullOrWhiteSpace(delegatedRole))
            throw new ArgumentException("Delegated role is required.", nameof(delegatedRole));
        if (string.IsNullOrWhiteSpace(branchCode))
            throw new ArgumentException("Branch code is required.", nameof(branchCode));
        if (string.IsNullOrWhiteSpace(delegationReason))
            throw new ArgumentException("Delegation reason is required.", nameof(delegationReason));

        if (effectiveToUtc <= effectiveFromUtc)
        {
            throw new ArgumentException("Effective end date must be after effective start date.", nameof(effectiveToUtc));
        }

        if (delegatorUserId.Equals(delegateeUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "SelfDelegationForbiddenInvariant",
                "BRPD Circular 12/2024 (10.2.4)",
                "An officer cannot delegate temporary authority to themselves.");
        }

        DelegatorUserId = delegatorUserId.Trim().ToUpperInvariant();
        DelegateeUserId = delegateeUserId.Trim().ToUpperInvariant();
        DelegatedRole = delegatedRole.Trim();
        BranchCode = branchCode.Trim();
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        DelegationReason = delegationReason.Trim();
        Status = DelegationStatus.PendingApproval;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Dual custody approval of temporary acting delegation by an independent manager.
    /// </summary>
    public void Approve(string approverUserId)
    {
        if (string.IsNullOrWhiteSpace(approverUserId))
            throw new ArgumentException("Approver user ID is required.", nameof(approverUserId));

        var normalizedApprover = approverUserId.Trim().ToUpperInvariant();
        if (normalizedApprover == DelegatorUserId || normalizedApprover == DelegateeUserId)
        {
            throw new InvariantViolationException(
                "DelegationDualCustodyInvariant",
                "BRPD Circular 12/2024 (10.2.4)",
                "Temporary authority delegation must be approved by an independent authorizing officer (excluding delegator and delegatee).");
        }

        if (Status != DelegationStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot approve delegation in status '{Status}'.");

        ApprovedByUserId = normalizedApprover;
        ApprovedAtUtc = DateTime.UtcNow;
        Status = DelegationStatus.Active;
    }

    /// <summary>
    /// Asserts that the acting delegation is currently valid and active.
    /// </summary>
    public void AssertIsActive(DateTime currentUtc)
    {
        if (Status != DelegationStatus.Active)
        {
            throw new InvariantViolationException(
                "InactiveDelegationInvariant",
                "BRPD Circular 12/2024 (10.2.5)",
                $"Delegation '{DelegationId}' is not active (Status: {Status}).");
        }

        if (currentUtc < EffectiveFromUtc)
        {
            throw new InvariantViolationException(
                "PrematureDelegationInvariant",
                "BRPD Circular 12/2024 (10.2.5)",
                $"Delegation '{DelegationId}' is not yet effective (Starts at {EffectiveFromUtc:yyyy-MM-dd HH:mm:ss} UTC).");
        }

        if (currentUtc > EffectiveToUtc)
        {
            Status = DelegationStatus.Expired;
            throw new InvariantViolationException(
                "ExpiredDelegationInvariant",
                "BRPD Circular 12/2024 (10.2.5)",
                $"Delegation '{DelegationId}' expired at {EffectiveToUtc:yyyy-MM-dd HH:mm:ss} UTC.");
        }
    }

    /// <summary>
    /// Revokes the delegation early before scheduled expiry date.
    /// </summary>
    public void Revoke(string revokedByUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(revokedByUserId))
            throw new ArgumentException("Revoking user ID is required.", nameof(revokedByUserId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Revocation reason is required.", nameof(reason));

        Status = DelegationStatus.Revoked;
        RevokedByUserId = revokedByUserId.Trim();
        RevocationReason = reason.Trim();
        RevokedAtUtc = DateTime.UtcNow;
    }
}
