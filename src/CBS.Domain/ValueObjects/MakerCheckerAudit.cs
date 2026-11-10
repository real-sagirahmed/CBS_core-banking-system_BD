using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.ValueObjects;

// ARCHITECTURAL INTENT: Separation of duties (4-eyes principle) is strictly enforced.
// Maker cannot approve own transaction. System Admins cannot execute financial transactions (BRPD Circular 12/2024 1.3.18, 9.1.1-xii, 10.3.5-vii, 10.3.5-x).
public sealed record MakerCheckerAudit
{
    public string MakerUserId { get; }
    public DateTime MakerTimestampUtc { get; }
    public string? CheckerUserId { get; private set; }
    public DateTime? CheckerTimestampUtc { get; private set; }
    public MakerCheckerStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }

    [System.Text.Json.Serialization.JsonConstructor]
    public MakerCheckerAudit(
        string makerUserId,
        DateTime makerTimestampUtc,
        string? checkerUserId,
        DateTime? checkerTimestampUtc,
        MakerCheckerStatus status,
        string? rejectionReason)
    {
        MakerUserId = makerUserId;
        MakerTimestampUtc = makerTimestampUtc;
        CheckerUserId = checkerUserId;
        CheckerTimestampUtc = checkerTimestampUtc;
        Status = status;
        RejectionReason = rejectionReason;
    }

    private MakerCheckerAudit(string makerUserId, DateTime makerTimestampUtc, MakerCheckerStatus status)
    {
        if (string.IsNullOrWhiteSpace(makerUserId))
            throw new ArgumentException("Maker User ID is required.", nameof(makerUserId));

        MakerUserId = makerUserId.Trim();
        MakerTimestampUtc = makerTimestampUtc;
        Status = status;
    }

    public static MakerCheckerAudit Initiate(string makerUserId)
    {
        return new MakerCheckerAudit(makerUserId, DateTime.UtcNow, MakerCheckerStatus.Submitted);
    }

    public void Authorize(string checkerUserId)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));

        string cleanChecker = checkerUserId.Trim();

        // INVARIANT ENFORCEMENT: Maker and Checker cannot be identical
        if (string.Equals(MakerUserId, cleanChecker, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18-i, 9.1.1-xii, 10.3.5-x)",
                $"User '{cleanChecker}' cannot authorize their own transaction. Dual-control 4-eyes principle violation.");
        }

        if (Status != MakerCheckerStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot authorize transaction in state '{Status}'. Must be 'Submitted'.");
        }

        CheckerUserId = cleanChecker;
        CheckerTimestampUtc = DateTime.UtcNow;
        Status = MakerCheckerStatus.Approved;
    }

    public void Reject(string checkerUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required.", nameof(reason));

        string cleanChecker = checkerUserId.Trim();

        if (string.Equals(MakerUserId, cleanChecker, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18-i, 9.1.1-xii)",
                $"User '{cleanChecker}' cannot reject their own transaction as checker.");
        }

        CheckerUserId = cleanChecker;
        CheckerTimestampUtc = DateTime.UtcNow;
        Status = MakerCheckerStatus.Rejected;
        RejectionReason = reason;
    }
}

public sealed record CifNumber
{
    public string Value { get; }

    public CifNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CIF number cannot be empty.", nameof(value));

        Value = value.Trim().ToUpperInvariant();
    }

    public override string ToString() => Value;
}
