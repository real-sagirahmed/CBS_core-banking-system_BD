// ARCHITECTURAL INTENT: Physical Payment Instrument Stock Book and Joint-Custody Inventory Aggregate
// adhering to Bangladesh Bank BRPD Circular No. 12 (1.5.6-ii, 3.11.1-iii..iv).

using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.PaymentInstruments;

public class PaymentInstrumentStockBook : AggregateRoot<string>
{
    public string BranchCode { get; private set; }
    public PaymentInstrumentType InstrumentType { get; private set; }
    public string Prefix { get; private set; }
    public long StartLeafNumber { get; private set; }
    public long EndLeafNumber { get; private set; }
    public int TotalLeaves { get; private set; }
    public int IssuedLeavesCount { get; private set; }
    public long NextAvailableLeafNumber { get; private set; }
    public string CustodianUserId { get; private set; }
    public string AllocatedByUserId { get; private set; }
    public DateTime AllocatedAtUtc { get; private set; }
    public bool IsExhausted => IssuedLeavesCount >= TotalLeaves;

#pragma warning disable CS8618
    private PaymentInstrumentStockBook() : base(string.Empty) { }
#pragma warning restore CS8618

    public PaymentInstrumentStockBook(
        string bookId,
        string branchCode,
        PaymentInstrumentType instrumentType,
        string prefix,
        long startLeafNumber,
        int totalLeaves,
        string custodianUserId,
        string allocatedByUserId) : base(bookId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(custodianUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(allocatedByUserId);

        if (startLeafNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(startLeafNumber), "Start leaf number must be positive.");

        if (totalLeaves is not (25 or 50 or 100))
        {
            throw new InvariantViolationException(
                "StockBookDenominationInvariant",
                "BRPD 1.5.6-ii",
                $"Stock book leaf count '{totalLeaves}' must be one of standard denominations: 25, 50, or 100.");
        }

        if (string.Equals(custodianUserId.Trim(), allocatedByUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "StockJointCustodianInvariant",
                "BRPD 3.11.1-iv",
                "Instrument stock book requires joint custodianship under dual control. Custodian and Allocator cannot be the same user.");
        }

        BranchCode = branchCode.Trim().ToUpperInvariant();
        InstrumentType = instrumentType;
        Prefix = prefix.Trim().ToUpperInvariant();
        StartLeafNumber = startLeafNumber;
        TotalLeaves = totalLeaves;
        EndLeafNumber = startLeafNumber + totalLeaves - 1;
        IssuedLeavesCount = 0;
        NextAvailableLeafNumber = startLeafNumber;
        CustodianUserId = custodianUserId.Trim().ToUpperInvariant();
        AllocatedByUserId = allocatedByUserId.Trim().ToUpperInvariant();
        AllocatedAtUtc = DateTime.UtcNow;
    }

    public string AllocateNextLeaf()
    {
        if (IsExhausted)
        {
            throw new InvariantViolationException(
                "StockBookExhaustedInvariant",
                "BRPD 1.5.6-ii",
                $"Stock book '{Id}' is completely exhausted. No leaves available for issuance.");
        }

        var leafFormatted = $"{Prefix}-{NextAvailableLeafNumber:D7}";
        NextAvailableLeafNumber++;
        IssuedLeavesCount++;
        return leafFormatted;
    }
}
