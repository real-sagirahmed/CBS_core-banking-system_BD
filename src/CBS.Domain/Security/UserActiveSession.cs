// ARCHITECTURAL INTENT: User Active Session tracking aggregate enforcing Single Active Session
// and terminal integrity invariants per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10 (10.3.5-viii, 10.1.4-j).

using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Security;

public class UserActiveSession : AggregateRoot<string>
{
    public string SessionId => Id;
    public string UserId { get; private set; } = string.Empty;
    public string TerminalIp { get; private set; } = string.Empty;
    public string AppServerIp { get; private set; } = string.Empty;
    public string BranchCode { get; private set; } = string.Empty;
    public DateTime LoginTimeUtc { get; private set; }
    public DateTime LastActivityUtc { get; private set; }
    public bool IsActive { get; private set; }
    public SessionTerminationReason? TerminationReason { get; private set; }
    public string? TerminationNotes { get; private set; }
    public DateTime? TerminatedAtUtc { get; private set; }

    // Concurrency tokens
    public byte[]? RowVersion { get; set; }
    public uint xmin { get; set; }

    // Private constructor for EF Core
    #pragma warning disable CS8618
    private UserActiveSession() : base(string.Empty) { }
    #pragma warning restore CS8618

    public UserActiveSession(
        string sessionId,
        string userId,
        string terminalIp,
        string appServerIp,
        string branchCode,
        DateTime? loginTimeUtc = null) : base(sessionId?.Trim() ?? string.Empty)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(terminalIp) || string.IsNullOrWhiteSpace(appServerIp))
        {
            throw new InvariantViolationException(
                "SessionTerminalIntegrityInvariant",
                "BRPD Circular 12/2024 (10.1.4-j, 10.3.5-viii)",
                "User Terminal IP and Application Server IP must be recorded for every authenticated CBS session.");
        }

        UserId = userId.Trim().ToUpperInvariant();
        TerminalIp = terminalIp.Trim();
        AppServerIp = appServerIp.Trim();
        BranchCode = string.IsNullOrWhiteSpace(branchCode) ? "HO-001" : branchCode.Trim().ToUpperInvariant();
        LoginTimeUtc = loginTimeUtc ?? DateTime.UtcNow;
        LastActivityUtc = LoginTimeUtc;
        IsActive = true;
    }

    public void TouchActivity(DateTime currentUtc)
    {
        if (!IsActive)
        {
            throw new InvariantViolationException(
                "SingleActiveSessionInvariant",
                "BRPD Circular 12/2024 (10.3.5-viii)",
                $"Cannot update activity for inactive or terminated session '{SessionId}'.");
        }

        LastActivityUtc = currentUtc;
    }

    public void TerminateSession(SessionTerminationReason reason, string? notes = null, DateTime? utcNow = null)
    {
        if (!IsActive)
            return; // Idempotent termination

        IsActive = false;
        TerminationReason = reason;
        TerminationNotes = notes;
        TerminatedAtUtc = utcNow ?? DateTime.UtcNow;
    }
}
