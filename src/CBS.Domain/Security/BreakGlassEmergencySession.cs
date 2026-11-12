using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Security;

// ARCHITECTURAL INTENT: Privileged Access Management (PAM) Emergency Break-Glass / Firecall Session.
// Governed strictly by Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10.1.5 & 10.3.5-v.
// Enforces mandatory dual-custody approval (two distinct officers, excluding requestor)
// and strict maximum 4-hour time-bounded validity window.

public sealed class BreakGlassEmergencySession : AggregateRoot<string>
{
    public string SessionId => Id;
    public string RequestorUserId { get; private set; } = null!;
    public string TargetRole { get; private set; } = null!;
    public string EmergencyJustification { get; private set; } = null!;
    public string? FirstApproverUserId { get; private set; }
    public DateTime? FirstApprovedAtUtc { get; private set; }
    public string? SecondApproverUserId { get; private set; }
    public DateTime? SecondApprovedAtUtc { get; private set; }
    public DateTime? SessionExpiresAtUtc { get; private set; }
    public BreakGlassStatus Status { get; private set; }
    public string? RevokedByUserId { get; private set; }
    public string? RevocationReason { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private BreakGlassEmergencySession() : base(string.Empty) { }
    #pragma warning restore CS8618

    public BreakGlassEmergencySession(
        string sessionId,
        string requestorUserId,
        string targetRole,
        string emergencyJustification,
        DateTime createdAtUtc) : base(sessionId.Trim().ToUpperInvariant())
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID is required.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(requestorUserId))
            throw new ArgumentException("Requestor user ID is required.", nameof(requestorUserId));
        if (string.IsNullOrWhiteSpace(targetRole))
            throw new ArgumentException("Target role is required.", nameof(targetRole));
        if (string.IsNullOrWhiteSpace(emergencyJustification))
            throw new ArgumentException("Emergency justification is required.", nameof(emergencyJustification));

        RequestorUserId = requestorUserId.Trim();
        TargetRole = targetRole.Trim();
        EmergencyJustification = emergencyJustification.Trim();
        Status = BreakGlassStatus.RequestedPendingApproval;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// First officer approval.
    /// </summary>
    public void ApproveFirstOfficer(string approverUserId)
    {
        if (string.IsNullOrWhiteSpace(approverUserId))
            throw new ArgumentException("Approver user ID is required.", nameof(approverUserId));

        if (approverUserId.Equals(RequestorUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "BreakGlassDualCustodyInvariant",
                "BRPD Circular 12/2024 (10.1.5)",
                "Requestor cannot approve their own emergency break-glass elevation.");
        }

        if (Status != BreakGlassStatus.RequestedPendingApproval)
            throw new InvalidOperationException($"Cannot apply first approval in status '{Status}'.");

        FirstApproverUserId = approverUserId.Trim();
        FirstApprovedAtUtc = DateTime.UtcNow;
        Status = BreakGlassStatus.PartiallyApprovedFirstOfficer;
    }

    /// <summary>
    /// Second officer approval, satisfying BreakGlassDualCustodyInvariant and activating the 4-hour window.
    /// </summary>
    public void ApproveSecondOfficer(string approverUserId)
    {
        if (string.IsNullOrWhiteSpace(approverUserId))
            throw new ArgumentException("Approver user ID is required.", nameof(approverUserId));

        if (approverUserId.Equals(RequestorUserId, StringComparison.OrdinalIgnoreCase) ||
            approverUserId.Equals(FirstApproverUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "BreakGlassDualCustodyInvariant",
                "BRPD Circular 12/2024 (10.1.5, 10.3.5-v)",
                "Emergency break-glass elevation strictly mandates dual custody approval from two distinct authorizing officers (excluding requestor).");
        }

        if (Status != BreakGlassStatus.PartiallyApprovedFirstOfficer)
            throw new InvalidOperationException("First officer approval must be completed before second officer authorization.");

        SecondApproverUserId = approverUserId.Trim();
        SecondApprovedAtUtc = DateTime.UtcNow;
        SessionExpiresAtUtc = DateTime.UtcNow.AddHours(4); // Strictly max 4-hour validity
        Status = BreakGlassStatus.ApprovedActive;
    }

    /// <summary>
    /// Validates session status and enforces the BreakGlassTimeWindowInvariant.
    /// </summary>
    public void AssertSessionValid(DateTime currentUtc)
    {
        if (Status != BreakGlassStatus.ApprovedActive)
        {
            throw new InvariantViolationException(
                "BreakGlassInvalidSessionInvariant",
                "BRPD Circular 12/2024 (10.1.5)",
                $"Emergency session '{SessionId}' is not active (Current status: {Status}).");
        }

        if (SessionExpiresAtUtc.HasValue && currentUtc > SessionExpiresAtUtc.Value)
        {
            Status = BreakGlassStatus.Expired;
            throw new InvariantViolationException(
                "BreakGlassTimeWindowInvariant",
                "BRPD Circular 12/2024 (10.1.5)",
                $"Emergency break-glass session '{SessionId}' expired at {SessionExpiresAtUtc.Value:yyyy-MM-dd HH:mm:ss} UTC (maximum 4-hour window elapsed).");
        }
    }

    /// <summary>
    /// Revokes the emergency access immediately.
    /// </summary>
    public void Revoke(string revokedByUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(revokedByUserId))
            throw new ArgumentException("Revoking user ID is required.", nameof(revokedByUserId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Revocation reason is required.", nameof(reason));

        Status = BreakGlassStatus.Revoked;
        RevokedByUserId = revokedByUserId.Trim();
        RevocationReason = reason.Trim();
        RevokedAtUtc = DateTime.UtcNow;
    }
}
