// ARCHITECTURAL INTENT: Back-Value Dated Transaction Request aggregate enforcing strict date window boundaries,
// dual-custody approval, and DOA limits per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 1 (1.3.4-i..iv).

using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.BackValue;

public class BackValueTransactionRequest : AggregateRoot<string>
{
    public const int MaxAllowedBackValueDays = 30;

    public string RequestId => Id;
    public BackValueTargetType TargetType { get; private set; }
    public string TargetIdentifier { get; private set; } = string.Empty;
    public string BranchCode { get; private set; } = string.Empty;
    public DateOnly BookingDate { get; private set; }
    public DateOnly ValueDate { get; private set; }
    public int DaysVariance { get; private set; }
    public Money Amount { get; private set; } = null!;
    public DebitCredit TransactionNature { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string RegulatoryReference { get; private set; } = string.Empty;
    public string MakerUserId { get; private set; } = string.Empty;
    public BackValueStatus Status { get; private set; }
    public string? CheckerUserId { get; private set; }
    public string? CheckerRemarks { get; private set; }
    public DateTime? ActionedAtUtc { get; private set; }
    public string? ExecutedVoucherNumber { get; private set; }
    public decimal? RetroactiveInterestCalculated { get; private set; }
    public string? RetroactiveInterestVoucherNumber { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Concurrency tokens
    public byte[]? RowVersion { get; set; }
    public uint xmin { get; set; }

    // Private constructor for EF Core
    #pragma warning disable CS8618
    private BackValueTransactionRequest() : base(string.Empty) { }
    #pragma warning restore CS8618

    public BackValueTransactionRequest(
        string requestId,
        BackValueTargetType targetType,
        string targetIdentifier,
        string branchCode,
        DateOnly bookingDate,
        DateOnly valueDate,
        Money amount,
        DebitCredit transactionNature,
        string reason,
        string regulatoryReference,
        string makerUserId,
        DateTime? createdAtUtc = null) : base(requestId?.Trim() ?? string.Empty)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));

        if (string.IsNullOrWhiteSpace(targetIdentifier))
            throw new ArgumentException("Target identifier (account number or GL account) cannot be empty.", nameof(targetIdentifier));

        if (string.IsNullOrWhiteSpace(makerUserId))
            throw new ArgumentException("Maker user ID cannot be empty.", nameof(makerUserId));

        if (amount is null || amount.Amount <= 0m)
            throw new ArgumentException("Transaction amount must be positive.", nameof(amount));

        if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(regulatoryReference))
        {
            throw new InvariantViolationException(
                "BackValueJustificationRequiredInvariant",
                "BRPD Circular 12/2024 (1.3.4-i, iii)",
                "Detailed business reason and regulatory authorization reference are mandatory for all back-value transaction requests.");
        }

        // ARCHITECTURAL INTENT: Value Date cannot be in the future
        if (valueDate > bookingDate)
        {
            throw new InvariantViolationException(
                "FutureValueDateProhibitedInvariant",
                "BRPD Circular 12/2024 (1.3.4-i)",
                $"Value Date '{valueDate:yyyy-MM-dd}' cannot be later than current Booking Date '{bookingDate:yyyy-MM-dd}' for standard financial entries.");
        }

        int variance = bookingDate.DayNumber - valueDate.DayNumber;

        // ARCHITECTURAL INTENT: Maximum allowed back-value threshold of 30 days
        if (variance > MaxAllowedBackValueDays)
        {
            throw new InvariantViolationException(
                "BackValueWindowExceededInvariant",
                "BRPD Circular 12/2024 (1.3.4-i)",
                $"Value Date '{valueDate:yyyy-MM-dd}' exceeds the maximum statutory back-value window of {MaxAllowedBackValueDays} calendar days relative to Booking Date '{bookingDate:yyyy-MM-dd}' (Variance: {variance} days).");
        }

        TargetType = targetType;
        TargetIdentifier = targetIdentifier.Trim().ToUpperInvariant();
        BranchCode = string.IsNullOrWhiteSpace(branchCode) ? "BR-001" : branchCode.Trim().ToUpperInvariant();
        BookingDate = bookingDate;
        ValueDate = valueDate;
        DaysVariance = variance;
        Amount = amount;
        TransactionNature = transactionNature;
        Reason = reason.Trim();
        RegulatoryReference = regulatoryReference.Trim();
        MakerUserId = makerUserId.Trim().ToUpperInvariant();
        Status = BackValueStatus.PendingApproval;
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
    }

    public void Approve(
        string checkerUserId,
        string? remarks,
        string executedVoucherNumber,
        decimal? retroactiveInterestCalculated,
        string? retroactiveInterestVoucherNumber,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker user ID cannot be empty.", nameof(checkerUserId));

        if (string.IsNullOrWhiteSpace(executedVoucherNumber))
            throw new ArgumentException("Executed voucher number must be provided on approval.", nameof(executedVoucherNumber));

        var normalizedChecker = checkerUserId.Trim().ToUpperInvariant();

        // ARCHITECTURAL INTENT: Dual-control requires independent authorizing officer (Maker != Checker)
        if (string.Equals(MakerUserId, normalizedChecker, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "BackValueMakerSelfApprovalInvariant",
                "BRPD Circular 12/2024 (1.3.4-i, iv)",
                $"Maker '{MakerUserId}' cannot authorize their own back-value dated transaction. Dual-control approval by an independent supervisor is mandatory.");
        }

        if (Status != BackValueStatus.PendingApproval)
        {
            throw new InvariantViolationException(
                "BackValueInvalidStatusTransitionInvariant",
                "BRPD Circular 12/2024 (1.3.4-i)",
                $"Cannot approve back-value request '{RequestId}' because its current status is '{Status}'.");
        }

        Status = BackValueStatus.Approved;
        CheckerUserId = normalizedChecker;
        CheckerRemarks = remarks;
        ExecutedVoucherNumber = executedVoucherNumber.Trim();
        RetroactiveInterestCalculated = retroactiveInterestCalculated;
        RetroactiveInterestVoucherNumber = retroactiveInterestVoucherNumber?.Trim();
        ActionedAtUtc = utcNow ?? DateTime.UtcNow;
    }

    public void Reject(string checkerUserId, string rejectionReason, DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker user ID cannot be empty.", nameof(checkerUserId));

        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException("Rejection reason cannot be empty.", nameof(rejectionReason));

        var normalizedChecker = checkerUserId.Trim().ToUpperInvariant();

        if (string.Equals(MakerUserId, normalizedChecker, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "BackValueMakerSelfApprovalInvariant",
                "BRPD Circular 12/2024 (1.3.4-i, iv)",
                $"Maker '{MakerUserId}' cannot reject their own back-value dated transaction. Independent authorization required.");
        }

        if (Status != BackValueStatus.PendingApproval)
        {
            throw new InvariantViolationException(
                "BackValueInvalidStatusTransitionInvariant",
                "BRPD Circular 12/2024 (1.3.4-i)",
                $"Cannot reject back-value request '{RequestId}' because its current status is '{Status}'.");
        }

        Status = BackValueStatus.Rejected;
        CheckerUserId = normalizedChecker;
        CheckerRemarks = rejectionReason.Trim();
        ActionedAtUtc = utcNow ?? DateTime.UtcNow;
    }
}
