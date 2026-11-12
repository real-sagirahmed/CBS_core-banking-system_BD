using System;
using System.Collections.Generic;
using System.Linq;
using CBS.Domain.Common;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Security;

// ARCHITECTURAL INTENT: Enterprise Password Lifecycle, History Lock & Expiry Engine.
// Governed strictly by Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10.1.3.
// Enforces mandatory default password change on first login (10.1.3-iv),
// 4-times password history retention preventing reuse (10.1.3-v),
// and strict 30-day password expiration tracking (10.1.3-vi).

public sealed class UserPasswordCredential : AggregateRoot<string>
{
    public const int MaxPasswordHistoryCount = 4;
    public const int DefaultPasswordValidityDays = 30;
    public const int MaxAllowedFailedAttempts = 5;

    public string UserId => Id;
    public string PasswordHash { get; private set; } = null!;
    public string Salt { get; private set; } = null!;
    public DateTime LastChangedAtUtc { get; private set; }
    public bool IsFirstLoginPendingChange { get; private set; }
    public int FailedAttemptCount { get; private set; }
    public bool IsLockedOut { get; private set; }
    public DateTime? LockoutExpiryUtc { get; private set; }
    public List<string> PasswordHistory { get; private set; } = new();

    #pragma warning disable CS8618
    private UserPasswordCredential() : base(string.Empty) { }
    #pragma warning restore CS8618

    public UserPasswordCredential(
        string userId,
        string passwordHash,
        string salt,
        bool isFirstLoginPendingChange,
        DateTime createdAtUtc) : base(userId.Trim().ToUpperInvariant())
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(salt))
            throw new ArgumentException("Salt is required.", nameof(salt));

        PasswordHash = passwordHash;
        Salt = salt;
        LastChangedAtUtc = createdAtUtc;
        IsFirstLoginPendingChange = isFirstLoginPendingChange;
        FailedAttemptCount = 0;
        IsLockedOut = false;
        PasswordHistory = new List<string>();
    }

    /// <summary>
    /// Changes password while enforcing PasswordHistoryFourTimesInvariant.
    /// </summary>
    public void ChangePassword(string newPasswordHash, string newSalt, DateTime currentUtc)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("New password hash is required.", nameof(newPasswordHash));
        if (string.IsNullOrWhiteSpace(newSalt))
            throw new ArgumentException("New salt is required.", nameof(newSalt));

        // ARCHITECTURAL INTENT: PasswordHistoryFourTimesInvariant prevents reuse of last 4 passwords
        if (newPasswordHash.Equals(PasswordHash, StringComparison.Ordinal) ||
            PasswordHistory.Any(h => h.Equals(newPasswordHash, StringComparison.Ordinal)))
        {
            throw new InvariantViolationException(
                "PasswordHistoryFourTimesInvariant",
                "BRPD Circular 12/2024 (10.1.3-v)",
                "New password cannot match any of the previous 4 passwords in history.");
        }

        // Add previous password to history
        PasswordHistory.Add(PasswordHash);
        if (PasswordHistory.Count > MaxPasswordHistoryCount)
        {
            PasswordHistory.RemoveAt(0);
        }

        PasswordHash = newPasswordHash;
        Salt = newSalt;
        LastChangedAtUtc = currentUtc;
        IsFirstLoginPendingChange = false;
        FailedAttemptCount = 0;
        IsLockedOut = false;
        LockoutExpiryUtc = null;
    }

    /// <summary>
    /// Enforces maximum 30-day password validity window per BRPD 10.1.3-vi.
    /// </summary>
    public void AssertPasswordNotExpired(DateTime currentUtc, int maxAgeDays = DefaultPasswordValidityDays)
    {
        if (IsFirstLoginPendingChange)
        {
            throw new InvariantViolationException(
                "FirstLoginPasswordChangeRequiredInvariant",
                "BRPD Circular 12/2024 (10.1.3-iv)",
                $"User '{UserId}' is logging in with a default password and must change password immediately.");
        }

        var age = (currentUtc - LastChangedAtUtc).TotalDays;
        if (age > maxAgeDays)
        {
            throw new InvariantViolationException(
                "PasswordExpiryThirtyDaysInvariant",
                "BRPD Circular 12/2024 (10.1.3-vi)",
                $"Password for user '{UserId}' has expired ({Math.Floor(age)} days old; maximum policy is {maxAgeDays} days). Password reset required.");
        }
    }

    public void RecordFailedAttempt(DateTime currentUtc)
    {
        FailedAttemptCount++;
        if (FailedAttemptCount >= MaxAllowedFailedAttempts)
        {
            IsLockedOut = true;
            LockoutExpiryUtc = currentUtc.AddMinutes(30);
        }
    }

    public void RecordSuccessfulLogin()
    {
        FailedAttemptCount = 0;
        IsLockedOut = false;
        LockoutExpiryUtc = null;
    }

    public void AdminResetPassword(string tempPasswordHash, string tempSalt, DateTime currentUtc)
    {
        PasswordHash = tempPasswordHash;
        Salt = tempSalt;
        LastChangedAtUtc = currentUtc;
        IsFirstLoginPendingChange = true; // Forces user to change on next login
        FailedAttemptCount = 0;
        IsLockedOut = false;
        LockoutExpiryUtc = null;
    }
}
