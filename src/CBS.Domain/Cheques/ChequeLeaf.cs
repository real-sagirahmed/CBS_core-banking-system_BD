using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Cheques;

// ARCHITECTURAL INTENT: Cheque Leaf fate tracking and strict stop-payment / duplicate payment prevention
// under Bangladesh Bank BRPD Circular No. 12 (1.5.7-vii to xiii).
public class ChequeLeaf : Entity<string>
{
    public string BookId { get; private set; }
    public AccountNumber AccountNumber { get; private set; }
    public long LeafNumber { get; private set; }
    public ChequeLeafStatus Status { get; private set; }
    public StopPaymentReason? StopReason { get; private set; }
    public string? StopNotes { get; private set; }
    public DateTime? StoppedAtUtc { get; private set; }
    public string? StoppedByBranch { get; private set; }
    public string? RevokedByOfficerId { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public string? PaidVoucherNumber { get; private set; }

#pragma warning disable CS8618
    private ChequeLeaf() : base(string.Empty) { }
#pragma warning restore CS8618

    internal ChequeLeaf(
        string leafId,
        string bookId,
        AccountNumber accountNumber,
        long leafNumber) : base(leafId)
    {
        if (string.IsNullOrWhiteSpace(leafId))
            throw new ArgumentException("Leaf ID is required.", nameof(leafId));
        if (string.IsNullOrWhiteSpace(bookId))
            throw new ArgumentException("Book ID is required.", nameof(bookId));
        ArgumentNullException.ThrowIfNull(accountNumber, nameof(accountNumber));

        BookId = bookId;
        AccountNumber = accountNumber;
        LeafNumber = leafNumber;
        Status = ChequeLeafStatus.Available;
    }

    public void AssertCanBeHonored()
    {
        if (Status == ChequeLeafStatus.Stopped)
        {
            throw new InvariantViolationException(
                "ChequeStoppedPaymentInvariant",
                "BRPD Circular 12/2024 (1.5.7-x)",
                $"Cheque Leaf '{LeafNumber}' has active Stop Payment instruction ({StopReason}: {StopNotes}). Payment prohibited.");
        }

        if (Status == ChequeLeafStatus.Paid)
        {
            throw new InvariantViolationException(
                "ChequeAlreadyPaidInvariant",
                "BRPD Circular 12/2024 (1.5.7-xii)",
                $"Cheque Leaf '{LeafNumber}' has already been paid on {PaidAtUtc:yyyy-MM-dd} (Voucher: {PaidVoucherNumber}). Duplicate payment prohibited.");
        }

        if (Status == ChequeLeafStatus.Cancelled)
        {
            throw new InvariantViolationException(
                "ChequeCancelledInvariant",
                "BRPD Circular 12/2024 (1.5.7-xii)",
                $"Cheque Leaf '{LeafNumber}' is Cancelled. Payment prohibited.");
        }
    }

    public void MarkStopped(StopPaymentReason reason, string notes, string branchCode)
    {
        if (Status == ChequeLeafStatus.Paid)
        {
            throw new InvalidOperationException($"Cannot stop payment on cheque leaf '{LeafNumber}' because it is already Paid.");
        }

        Status = ChequeLeafStatus.Stopped;
        StopReason = reason;
        StopNotes = notes;
        StoppedByBranch = branchCode;
        StoppedAtUtc = DateTime.UtcNow;
    }

    public void RevokeStop(string officerId, string reason)
    {
        if (Status != ChequeLeafStatus.Stopped)
        {
            throw new InvalidOperationException($"Cannot revoke stop payment on leaf '{LeafNumber}' because status is '{Status}'.");
        }

        Status = ChequeLeafStatus.Available;
        RevokedByOfficerId = officerId;
        RevokedAtUtc = DateTime.UtcNow;
        StopNotes = $"Stop revoked by {officerId}: {reason}";
    }

    public void MarkPaid(string voucherNumber)
    {
        AssertCanBeHonored();
        Status = ChequeLeafStatus.Paid;
        PaidAtUtc = DateTime.UtcNow;
        PaidVoucherNumber = voucherNumber;
    }
}
