using CBS.Domain.Common;
using CBS.Domain.Enums;

namespace CBS.Domain.SecurityAudit;

/// <summary>
/// User Session & Administrative Access Control Aggregate Root conforming to Bangladesh Bank BRPD Circular 12 (Module 10).
/// Invariants:
/// 1. Concurrent active sessions for the same user are strictly blocked (BRPD 10.3.5-viii).
/// 2. Super/Admin users are strictly prohibited from performing financial transactions (BRPD 10.3.5-vii).
/// </summary>
public sealed class UserSession : AggregateRoot<string>
{
    public string SessionId => Id;
    public string UserId { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public string BranchCode { get; private set; } = null!;
    public string TerminalIp { get; private set; } = null!;
    public DateTime LoggedInAtUtc { get; private set; }
    public DateTime? LoggedOutAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    private UserSession(string sessionId) : base(sessionId) { }

    public static UserSession StartSession(
        string sessionId,
        string userId,
        UserRole role,
        string branchCode,
        string terminalIp,
        bool hasExistingActiveSession)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID is required.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        // BRPD 10.3.5-viii: CBS shall block multiple session initiated by single user
        if (hasExistingActiveSession)
        {
            throw new InvalidOperationException($"Security Invariant Violation [BRPD 10.3.5-viii]: Active session already exists for user '{userId}'. Multiple/concurrent login is blocked.");
        }

        return new UserSession(sessionId.Trim())
        {
            UserId = userId.Trim(),
            Role = role,
            BranchCode = branchCode.Trim(),
            TerminalIp = terminalIp.Trim(),
            LoggedInAtUtc = DateTime.UtcNow,
            IsActive = true
        };
    }

    public bool CanPerformFinancialTransactions => IsActive && Role != UserRole.SystemAdmin && Role != UserRole.ComplianceAuditor;

    /// <summary>
    /// Validates whether the user's role is permitted to perform a financial transaction.
    /// Invariant: Super/Admin and Audit users in CBS shall NOT be authorized to perform or approve any financial transactions (BRPD 10.3.5-vii).
    /// </summary>
    public void AssertCanPerformFinancialTransaction()
    {
        if (!IsActive)
            throw new InvalidOperationException("Session is expired or inactive.");

        if (Role == UserRole.SystemAdmin || Role == UserRole.ComplianceAuditor)
        {
            throw new InvalidOperationException($"Security Invariant Violation [BRPD 10.3.5-vii]: Super/Admin users in CBS shall not be authorized to perform any financial transactions. Role '{Role}' is blocked from financial transactions.");
        }
    }

    public void TerminateSession()
    {
        IsActive = false;
        LoggedOutAtUtc = DateTime.UtcNow;
    }
}
