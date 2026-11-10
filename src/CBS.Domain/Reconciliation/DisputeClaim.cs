using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Reconciliation;

/// <summary>
/// Aggregate Root representing an ADC / Payment Switch Customer Dispute Claim.
/// Governed strictly by Bangladesh Bank BRPD Circular 12 Clause 1.4.1-vii and 7.2.1-xx.
/// </summary>
public sealed class DisputeClaim : AggregateRoot<string>
{
    // ARCHITECTURAL INTENT: Original STAN & RRN Dispute Trace Invariant (BRPD 1.4.1-vii).
    // The dispute refund/rectification voucher MUST strictly reference and bind to the exact
    // same original System Trace Audit Number (STAN) and Retrieval Reference Number (RRN).
    // Arbitrary or newly minted transaction references for dispute refunds are strictly forbidden.

    public string ClaimId => Id;
    public string OriginalTransactionId { get; private set; } = null!;
    public string OriginalStan { get; private set; } = null!;
    public string OriginalRrn { get; private set; } = null!;
    public AccountNumber AccountNumber { get; private set; } = null!;
    public Money DisputedAmount { get; private set; } = null!;
    public AdcChannelType Channel { get; private set; }
    public string DisputeReason { get; private set; } = null!;
    public DisputeStatus Status { get; private set; }
    public string? RectificationVoucherNumber { get; private set; }
    public string? ResolvedByOfficerId { get; private set; }
    public string InvestigationNotes { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    // Parameterless constructor for EF Core ORM materialization
    private DisputeClaim() : base(string.Empty) { }

    private DisputeClaim(string claimId) : base(claimId) { }

    public static DisputeClaim LogClaim(
        string claimId,
        string originalTransactionId,
        string originalStan,
        string originalRrn,
        AccountNumber accountNumber,
        Money disputedAmount,
        AdcChannelType channel,
        string disputeReason)
    {
        if (string.IsNullOrWhiteSpace(claimId))
            throw new ArgumentException("Claim ID is required.", nameof(claimId));
        if (string.IsNullOrWhiteSpace(originalTransactionId))
            throw new ArgumentException("Original Transaction ID is required.", nameof(originalTransactionId));
        if (string.IsNullOrWhiteSpace(originalStan))
            throw new ArgumentException("Original STAN is required.", nameof(originalStan));
        if (string.IsNullOrWhiteSpace(originalRrn))
            throw new ArgumentException("Original RRN is required.", nameof(originalRrn));
        if (disputedAmount.Amount <= 0)
            throw new ArgumentException("Disputed amount must be positive.", nameof(disputedAmount));

        return new DisputeClaim(claimId.Trim())
        {
            OriginalTransactionId = originalTransactionId.Trim(),
            OriginalStan = originalStan.Trim(),
            OriginalRrn = originalRrn.Trim(),
            AccountNumber = accountNumber,
            DisputedAmount = disputedAmount,
            Channel = channel,
            DisputeReason = disputeReason.Trim(),
            Status = DisputeStatus.Logged,
            InvestigationNotes = "Claim registered and queued for switch audit log verification.",
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void ResolveWithRefund(
        string checkerUserId,
        string rectificationVoucherNumber,
        string providedStan,
        string providedRrn,
        string investigationNotes)
    {
        if (Status != DisputeStatus.Logged && Status != DisputeStatus.UnderInvestigation)
            throw new InvalidOperationException($"Cannot resolve dispute in status '{Status}'.");
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker Officer User ID is required.", nameof(checkerUserId));
        if (string.IsNullOrWhiteSpace(rectificationVoucherNumber))
            throw new ArgumentException("Rectification Voucher Number is required.", nameof(rectificationVoucherNumber));

        // Invariant Lock: BRPD 1.4.1-vii Strict Trace Number Matching
        if (!string.Equals(OriginalStan, providedStan?.Trim(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(OriginalRrn, providedRrn?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Security Invariant Violation [BRPD 1.4.1-vii]: Dispute refund MUST utilize the exact original STAN and RRN. " +
                $"Expected STAN '{OriginalStan}' / RRN '{OriginalRrn}', but received STAN '{providedStan}' / RRN '{providedRrn}'.");
        }

        Status = DisputeStatus.RefundApproved;
        ResolvedByOfficerId = checkerUserId.Trim();
        RectificationVoucherNumber = rectificationVoucherNumber.Trim();
        InvestigationNotes = investigationNotes.Trim();
        ResolvedAtUtc = DateTime.UtcNow;
    }

    public void RejectDispute(string checkerUserId, string reason)
    {
        if (Status != DisputeStatus.Logged && Status != DisputeStatus.UnderInvestigation)
            throw new InvalidOperationException($"Cannot reject dispute in status '{Status}'.");

        Status = DisputeStatus.Rejected;
        ResolvedByOfficerId = checkerUserId.Trim();
        InvestigationNotes = $"Rejected: {reason.Trim()}";
        ResolvedAtUtc = DateTime.UtcNow;
    }
}
