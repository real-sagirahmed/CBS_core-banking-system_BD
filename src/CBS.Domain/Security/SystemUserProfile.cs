using System.Security.Cryptography;
using System.Text;
using CBS.Domain.Common;
using CBS.Domain.Exceptions;
using CBS.Domain.Enums;
using CBS.Domain.Security.ValueObjects;

namespace CBS.Domain.Security;

// ARCHITECTURAL INTENT: CBS System User Profile & Security Credential Lifecycle
// Enforces Bangladesh Bank BRPD Circular 12/2024:
// - Module 10.1.3: Password management (4-history retention, 30-day rotation, first-login change)
// - Module 10.1.4: User information & DOA limits
// - Module 10.3.5-vii: Super/Admin financial transaction prohibition
// - Module 10.3.5-viii: Single active session enforcement / concurrent session blocking
// - Module 9.1.1-xviii: Login attempt telemetry & prior attempt alerting
public class SystemUserProfile : AggregateRoot<string>
{
    public string EmployeeId { get; private set; }
    public string FullName { get; private set; }
    public string Email { get; private set; }
    public string BranchCode { get; private set; }
    public UserGroupType UserGroup { get; private set; }
    public List<string> AssignedRoles { get; private set; } = new();
    public string PasswordHash { get; private set; }
    public bool MustChangePasswordOnNextLogin { get; private set; }
    public DateTime PasswordLastChangedAtUtc { get; private set; }
    public int PasswordExpiryDays { get; private set; }
    public UserAccountStatus Status { get; private set; }
    public string? ActiveSessionToken { get; private set; }
    public string? LastLoginWorkstationIp { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public DateTime? PreviousSuccessfulLoginAtUtc { get; private set; }
    public string? PreviousSuccessfulLoginIp { get; private set; }
    public DateTime? PreviousFailedLoginAtUtc { get; private set; }
    public string? PreviousFailedLoginIp { get; private set; }
    public int ConsecutiveFailedLoginAttempts { get; private set; }
    public List<PasswordHistoryEntry> PasswordHistory { get; private set; } = new();
    public DoaLimitProfile DoaLimits { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedByUserId { get; private set; }
    public string? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }

    #pragma warning disable CS8618
    protected SystemUserProfile() : base(null!) { }
    #pragma warning restore CS8618

    public SystemUserProfile(
        string userId,
        string employeeId,
        string fullName,
        string email,
        string branchCode,
        UserGroupType userGroup,
        List<string> assignedRoles,
        string initialPlainPassword,
        DoaLimitProfile? doaLimits,
        string createdByUserId,
        DateTime createdAtUtc) : base(userId.Trim().ToUpperInvariant())
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Employee ID is required.", nameof(employeeId));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(initialPlainPassword))
            throw new ArgumentException("Initial password is required.", nameof(initialPlainPassword));

        EmployeeId = employeeId.Trim();
        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        BranchCode = branchCode.Trim();
        UserGroup = userGroup;
        AssignedRoles = assignedRoles?.Distinct().ToList() ?? new List<string>();
        PasswordHash = ComputeSha256(initialPlainPassword);
        MustChangePasswordOnNextLogin = true; // BRPD 10.1.3-iv: default password must be changed at first login
        PasswordLastChangedAtUtc = createdAtUtc;
        PasswordExpiryDays = 30; // BRPD 10.1.3-vi: reset at least every 30 days
        Status = UserAccountStatus.Active;
        CreatedByUserId = createdByUserId.Trim();
        CreatedAtUtc = createdAtUtc;

        // If administrator, DOA limit is strictly zero (BRPD 10.3.5-vii)
        DoaLimits = userGroup == UserGroupType.Administrator
            ? DoaLimitProfile.Administrator(branchCode)
            : (doaLimits ?? DoaLimitProfile.DefaultTeller(branchCode));

        // Seed initial password to history
        PasswordHistory.Add(new PasswordHistoryEntry(PasswordHash, createdAtUtc));
    }

    public static string ComputeSha256(string rawPassword)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawPassword));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool VerifyPassword(string plainPassword)
    {
        string hash = ComputeSha256(plainPassword);
        return string.Equals(PasswordHash, hash, StringComparison.Ordinal);
    }

    public void ChangePassword(string currentPlainPassword, string newPlainPassword, DateTime utcNow)
    {
        if (!VerifyPassword(currentPlainPassword))
        {
            throw new InvariantViolationException(
                "InvalidCurrentPasswordInvariant",
                "BRPD Circular 12/2024 (10.1.3)",
                "Current password provided does not match records.");
        }

        if (string.IsNullOrWhiteSpace(newPlainPassword) || newPlainPassword.Length < 8)
        {
            throw new InvariantViolationException(
                "WeakPasswordInvariant",
                "BRPD Circular 12/2024 (10.1.3)",
                "Password must be at least 8 characters long.");
        }

        string newHash = ComputeSha256(newPlainPassword);

        // BRPD 10.1.3-v: Password history shall be maintained at least four times
        // Disallow current password reuse
        if (string.Equals(PasswordHash, newHash, StringComparison.Ordinal))
        {
            throw new InvariantViolationException(
                "PasswordHistoryReuseInvariant",
                "BRPD Circular 12/2024 (10.1.3-v)",
                "New password cannot be identical to the current active password.");
        }

        // Disallow last 4 historical passwords
        var recentHistory = PasswordHistory.OrderByDescending(p => p.CreatedAtUtc).Take(4).ToList();
        if (recentHistory.Any(h => string.Equals(h.PasswordHash, newHash, StringComparison.Ordinal)))
        {
            throw new InvariantViolationException(
                "PasswordHistoryReuseInvariant",
                "BRPD Circular 12/2024 (10.1.3-v)",
                "New password cannot match any of the last 4 previous passwords.");
        }

        // Archive current password into history
        PasswordHistory.Insert(0, new PasswordHistoryEntry(PasswordHash, utcNow));
        if (PasswordHistory.Count > 10)
        {
            PasswordHistory = PasswordHistory.Take(10).ToList();
        }

        PasswordHash = newHash;
        PasswordLastChangedAtUtc = utcNow;
        MustChangePasswordOnNextLogin = false;
    }

    public bool IsPasswordExpired(DateTime utcNow)
    {
        return (utcNow - PasswordLastChangedAtUtc).TotalDays >= PasswordExpiryDays;
    }

    public string RecordLoginAttempt(bool isSuccess, string workstationIp, DateTime utcNow, string? failureReason = null)
    {
        if (isSuccess)
        {
            PreviousSuccessfulLoginAtUtc = LastLoginAtUtc;
            PreviousSuccessfulLoginIp = LastLoginWorkstationIp;
            LastLoginAtUtc = utcNow;
            LastLoginWorkstationIp = workstationIp;
            ConsecutiveFailedLoginAttempts = 0;

            // BRPD 10.3.5-viii: CBS shall be capable to block multiple session initiated by single user
            ActiveSessionToken = Guid.NewGuid().ToString("N");
            return ActiveSessionToken;
        }
        else
        {
            PreviousFailedLoginAtUtc = utcNow;
            PreviousFailedLoginIp = workstationIp;
            ConsecutiveFailedLoginAttempts++;

            if (ConsecutiveFailedLoginAttempts >= 5)
            {
                Status = UserAccountStatus.Locked;
            }

            return string.Empty;
        }
    }

    public void TerminateSession()
    {
        ActiveSessionToken = null;
    }

    public void UpdateDoaLimits(DoaLimitProfile newLimits)
    {
        ArgumentNullException.ThrowIfNull(newLimits, nameof(newLimits));

        // BRPD 10.3.5-vii: Super/Admin users in CBS shall not be authorized to perform any financial transactions
        if (UserGroup == UserGroupType.Administrator)
        {
            throw new InvariantViolationException(
                "AdminFinancialTxnBlockedInvariant",
                "BRPD Circular 12/2024 (10.3.5-vii)",
                "Administrator group cannot be assigned non-zero financial transaction limits.");
        }

        DoaLimits = newLimits;
    }

    public void AssertCanPerformFinancialTransaction()
    {
        // BRPD 10.3.5-vii: Super/Admin users in CBS shall not be authorized to perform any financial transactions
        if (UserGroup == UserGroupType.Administrator)
        {
            throw new InvariantViolationException(
                "AdminFinancialTxnBlockedInvariant",
                "BRPD Circular 12/2024 (10.3.5-vii)",
                $"User '{Id}' belongs to 'Administrator' group and is strictly forbidden from executing or approving financial transactions.");
        }
    }

    public void MarkApproved(string checkerUserId, DateTime approvedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker user ID is required.", nameof(checkerUserId));

        // BRPD 10.3.5-x & 1.3.18: Dual Control
        if (checkerUserId.Equals(CreatedByUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (10.3.5-x, 1.3.18)",
                $"Maker '{CreatedByUserId}' cannot approve their own user onboarding profile.");
        }

        ApprovedByUserId = checkerUserId.Trim();
        ApprovedAtUtc = approvedAtUtc;
        Status = UserAccountStatus.Active;
    }
}
