using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Security;

// ARCHITECTURAL INTENT: Delegation of Authority (DOA) & Multi-Tier Financial Transaction Limit Matrix.
// Governed strictly by Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10.1.5-d & 10.2.3.
// Enforces mandatory TransactionDoaCeilingInvariant preventing any single teller or officer from executing
// or authorising transactions above their approved ceiling without higher supervisor dual-authorization.

public sealed class UserDoaProfile : AggregateRoot<string>
{
    public string ProfileId => Id;
    public string UserId { get; private set; } = null!;
    public string RoleName { get; private set; } = null!;
    public string BranchCode { get; private set; } = null!;
    public Money MaxLocalDebitLimit { get; private set; } = null!;
    public Money MaxLocalCreditLimit { get; private set; } = null!;
    public Money MaxInterBranchDebitLimit { get; private set; } = null!;
    public Money MaxInterBranchCreditLimit { get; private set; } = null!;
    public Money MaxGlAdjustmentLimit { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public string AssignedByUserId { get; private set; } = null!;
    public DateTime AssignedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private UserDoaProfile() : base(string.Empty) { }
    #pragma warning restore CS8618

    public UserDoaProfile(
        string profileId,
        string userId,
        string roleName,
        string branchCode,
        Money maxLocalDebitLimit,
        Money maxLocalCreditLimit,
        Money maxInterBranchDebitLimit,
        Money maxInterBranchCreditLimit,
        Money maxGlAdjustmentLimit,
        string assignedByUserId,
        DateTime assignedAtUtc) : base(profileId.Trim().ToUpperInvariant())
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile ID is required.", nameof(profileId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("Role name is required.", nameof(roleName));
        if (string.IsNullOrWhiteSpace(branchCode))
            throw new ArgumentException("Branch code is required.", nameof(branchCode));
        if (string.IsNullOrWhiteSpace(assignedByUserId))
            throw new ArgumentException("Assigned by user ID is required.", nameof(assignedByUserId));

        ArgumentNullException.ThrowIfNull(maxLocalDebitLimit, nameof(maxLocalDebitLimit));
        ArgumentNullException.ThrowIfNull(maxLocalCreditLimit, nameof(maxLocalCreditLimit));
        ArgumentNullException.ThrowIfNull(maxInterBranchDebitLimit, nameof(maxInterBranchDebitLimit));
        ArgumentNullException.ThrowIfNull(maxInterBranchCreditLimit, nameof(maxInterBranchCreditLimit));
        ArgumentNullException.ThrowIfNull(maxGlAdjustmentLimit, nameof(maxGlAdjustmentLimit));

        UserId = userId.Trim().ToUpperInvariant();
        RoleName = roleName.Trim();
        BranchCode = branchCode.Trim();
        MaxLocalDebitLimit = maxLocalDebitLimit;
        MaxLocalCreditLimit = maxLocalCreditLimit;
        MaxInterBranchDebitLimit = maxInterBranchDebitLimit;
        MaxInterBranchCreditLimit = maxInterBranchCreditLimit;
        MaxGlAdjustmentLimit = maxGlAdjustmentLimit;
        IsActive = true;
        AssignedByUserId = assignedByUserId.Trim();
        AssignedAtUtc = assignedAtUtc;
    }

    /// <summary>
    /// Validates financial transaction amount against the user's approved Delegation of Authority limit.
    /// </summary>
    public void AssertTransactionAllowed(TransactionLimitType limitType, Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));

        if (!IsActive)
        {
            throw new InvariantViolationException(
                "InactiveDoaProfileInvariant",
                "BRPD Circular 12/2024 (10.1.5-d)",
                $"User '{UserId}' does not have an active Delegation of Authority profile.");
        }

        Money applicableCeiling = limitType switch
        {
            TransactionLimitType.LocalDebit => MaxLocalDebitLimit,
            TransactionLimitType.LocalCredit => MaxLocalCreditLimit,
            TransactionLimitType.InterBranchDebit => MaxInterBranchDebitLimit,
            TransactionLimitType.InterBranchCredit => MaxInterBranchCreditLimit,
            TransactionLimitType.GlManualAdjustment => MaxGlAdjustmentLimit,
            _ => throw new ArgumentOutOfRangeException(nameof(limitType), limitType, "Unknown limit type.")
        };

        if (amount.Amount > applicableCeiling.Amount)
        {
            throw new InvariantViolationException(
                "TransactionDoaCeilingInvariant",
                "BRPD Circular 12/2024 (10.1.5-d, 10.2.3)",
                $"Transaction amount of {amount.Amount:N2} BDT exceeds user '{UserId}' approved {limitType} limit of {applicableCeiling.Amount:N2} BDT. Higher supervisor dual-authorization required.");
        }
    }

    /// <summary>
    /// Updates DOA limits with audit trail.
    /// </summary>
    public void UpdateLimits(
        Money maxLocalDebitLimit,
        Money maxLocalCreditLimit,
        Money maxInterBranchDebitLimit,
        Money maxInterBranchCreditLimit,
        Money maxGlAdjustmentLimit,
        string updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(updatedByUserId))
            throw new ArgumentException("Updated by user ID is required.", nameof(updatedByUserId));

        ArgumentNullException.ThrowIfNull(maxLocalDebitLimit, nameof(maxLocalDebitLimit));
        ArgumentNullException.ThrowIfNull(maxLocalCreditLimit, nameof(maxLocalCreditLimit));
        ArgumentNullException.ThrowIfNull(maxInterBranchDebitLimit, nameof(maxInterBranchDebitLimit));
        ArgumentNullException.ThrowIfNull(maxInterBranchCreditLimit, nameof(maxInterBranchCreditLimit));
        ArgumentNullException.ThrowIfNull(maxGlAdjustmentLimit, nameof(maxGlAdjustmentLimit));

        MaxLocalDebitLimit = maxLocalDebitLimit;
        MaxLocalCreditLimit = maxLocalCreditLimit;
        MaxInterBranchDebitLimit = maxInterBranchDebitLimit;
        MaxInterBranchCreditLimit = maxInterBranchCreditLimit;
        MaxGlAdjustmentLimit = maxGlAdjustmentLimit;
        AssignedByUserId = updatedByUserId.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
