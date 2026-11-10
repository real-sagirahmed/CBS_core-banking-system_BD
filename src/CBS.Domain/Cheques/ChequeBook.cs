using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Cheques;

// ARCHITECTURAL INTENT: Cheque Book Requisition, Stock Management, Sequential Leaf Generation,
// and Delivery Tracking under Bangladesh Bank BRPD Circular No. 12 (1.5.7-i to xv, 3.1.14).
public class ChequeBook : AggregateRoot<string>
{
    public AccountNumber AccountNumber { get; private set; }
    public string Prefix { get; private set; }
    public long StartLeafNumber { get; private set; }
    public long EndLeafNumber { get; private set; }
    public int TotalLeaves { get; private set; }
    public ChequeBookStatus Status { get; private set; }
    public string BranchCode { get; private set; }
    public DateOnly RequisitionDate { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private readonly List<ChequeLeaf> _leaves = [];
    public IReadOnlyCollection<ChequeLeaf> Leaves => _leaves.AsReadOnly();

#pragma warning disable CS8618
    private ChequeBook() : base(string.Empty) { }
#pragma warning restore CS8618

    public ChequeBook(
        string bookId,
        AccountNumber accountNumber,
        string prefix,
        long startLeafNumber,
        int totalLeaves,
        string branchCode,
        DateOnly requisitionDate) : base(bookId)
    {
        if (string.IsNullOrWhiteSpace(bookId))
            throw new ArgumentException("Book ID is required.", nameof(bookId));
        ArgumentNullException.ThrowIfNull(accountNumber, nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix is required.", nameof(prefix));
        if (startLeafNumber <= 0)
            throw new ArgumentException("Start leaf number must be positive.", nameof(startLeafNumber));
        if (totalLeaves is not (10 or 20 or 25 or 50 or 100))
        {
            throw new InvariantViolationException(
                "ChequeBookLeafCountInvariant",
                "BRPD Circular 12/2024 (1.5.7-iii)",
                $"Cheque book leaf count '{totalLeaves}' must be one of standard denominations: 10, 20, 25, 50, or 100.");
        }

        AccountNumber = accountNumber;
        Prefix = prefix.Trim().ToUpperInvariant();
        StartLeafNumber = startLeafNumber;
        TotalLeaves = totalLeaves;
        EndLeafNumber = startLeafNumber + totalLeaves - 1;
        BranchCode = branchCode;
        RequisitionDate = requisitionDate;
        Status = ChequeBookStatus.Requested;
        CreatedAtUtc = DateTime.UtcNow;

        // Sequentially generate leaves
        for (long i = StartLeafNumber; i <= EndLeafNumber; i++)
        {
            _leaves.Add(new ChequeLeaf(
                leafId: $"{bookId}-{i}",
                bookId: bookId,
                accountNumber: accountNumber,
                leafNumber: i));
        }
    }

    public void MarkDelivered()
    {
        Status = ChequeBookStatus.DeliveredToCustomer;
        DeliveredAtUtc = DateTime.UtcNow;
    }

    public ChequeLeaf GetLeaf(long leafNumber)
    {
        var leaf = _leaves.FirstOrDefault(l => l.LeafNumber == leafNumber);
        if (leaf is null)
        {
            throw new KeyNotFoundException($"Cheque leaf '{leafNumber}' does not belong to Cheque Book '{Id}'.");
        }
        return leaf;
    }

    public void StopLeaf(long leafNumber, StopPaymentReason reason, string notes, string branchCode)
    {
        var leaf = GetLeaf(leafNumber);
        leaf.MarkStopped(reason, notes, branchCode);
    }

    public void RevokeStopPayment(long leafNumber, string officerId, string reason)
    {
        var leaf = GetLeaf(leafNumber);
        leaf.RevokeStop(officerId, reason);
    }

    public void MarkLeafPaid(long leafNumber, string voucherNumber)
    {
        var leaf = GetLeaf(leafNumber);
        leaf.MarkPaid(voucherNumber);
    }
}
