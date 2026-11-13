// ARCHITECTURAL INTENT: Linked Cascade Transaction Reversal aggregate recording contra-reversal of
// original voucher and all auxiliary entries (fees, commissions, VAT) per BRPD Circular No. 12 (13 May 2024) Module 1 (1.3.6-i..iii).

using System;
using CBS.Domain.Common;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.BackValue;

public class LinkedTransactionReversalRecord : AggregateRoot<string>
{
    public string ReversalRecordId => Id;
    public string OriginalVoucherId { get; private set; } = string.Empty;
    public string OriginalVoucherNumber { get; private set; } = string.Empty;
    public string ReversalReason { get; private set; } = string.Empty;
    public string ReversedByUserId { get; private set; } = string.Empty;
    public string AuthorizerUserId { get; private set; } = string.Empty;
    public DateTime ReversedAtUtc { get; private set; }
    public string ParentReversalVoucherNumber { get; private set; } = string.Empty;
    public string LinkedReversalsJson { get; private set; } = "[]";
    public Money TotalReversedAmount { get; private set; } = null!;
    public bool IsCascadeComplete { get; private set; }

    // Concurrency tokens
    public byte[]? RowVersion { get; set; }
    public uint xmin { get; set; }

    // Private constructor for EF Core
    #pragma warning disable CS8618
    private LinkedTransactionReversalRecord() : base(string.Empty) { }
    #pragma warning restore CS8618

    public LinkedTransactionReversalRecord(
        string reversalRecordId,
        string originalVoucherId,
        string originalVoucherNumber,
        string reversalReason,
        string reversedByUserId,
        string authorizerUserId,
        string parentReversalVoucherNumber,
        string linkedReversalsJson,
        Money totalReversedAmount,
        DateTime? reversedAtUtc = null) : base(reversalRecordId?.Trim() ?? string.Empty)
    {
        if (string.IsNullOrWhiteSpace(reversalRecordId))
            throw new ArgumentException("Reversal record ID cannot be empty.", nameof(reversalRecordId));

        if (string.IsNullOrWhiteSpace(originalVoucherId) || string.IsNullOrWhiteSpace(originalVoucherNumber))
            throw new ArgumentException("Original voucher identifiers cannot be empty.", nameof(originalVoucherId));

        if (string.IsNullOrWhiteSpace(reversalReason))
            throw new ArgumentException("Reversal reason is mandatory.", nameof(reversalReason));

        if (string.IsNullOrWhiteSpace(reversedByUserId) || string.IsNullOrWhiteSpace(authorizerUserId))
            throw new ArgumentException("Maker and Checker user IDs are required for reversal.", nameof(reversedByUserId));

        if (totalReversedAmount is null || totalReversedAmount.Amount <= 0m)
            throw new ArgumentException("Total reversed amount must be positive.", nameof(totalReversedAmount));

        OriginalVoucherId = originalVoucherId.Trim();
        OriginalVoucherNumber = originalVoucherNumber.Trim();
        ReversalReason = reversalReason.Trim();
        ReversedByUserId = reversedByUserId.Trim().ToUpperInvariant();
        AuthorizerUserId = authorizerUserId.Trim().ToUpperInvariant();
        ParentReversalVoucherNumber = parentReversalVoucherNumber.Trim();
        LinkedReversalsJson = string.IsNullOrWhiteSpace(linkedReversalsJson) ? "[]" : linkedReversalsJson.Trim();
        TotalReversedAmount = totalReversedAmount;
        IsCascadeComplete = true;
        ReversedAtUtc = reversedAtUtc ?? DateTime.UtcNow;
    }
}
