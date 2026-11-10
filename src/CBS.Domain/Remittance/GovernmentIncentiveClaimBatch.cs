using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Remittance;

// ARCHITECTURAL INTENT: Aggregates disbursed 2.5% Wage Earners' Government Cash Incentives
// into periodic claim batches for statutory reimbursement from Bangladesh Bank (BRPD 2.2.3 & BB FE Circular No. 01/2022).
public class GovernmentIncentiveClaimBatch : AggregateRoot<Guid>
{
    public string BatchNumber { get; private set; }
    public string ClaimPeriod { get; private set; } // e.g. "2026-11"
    public int TotalTransactionCount { get; private set; }
    public Money TotalClaimedIncentiveBdt { get; private set; }
    public IncentiveClaimBatchStatus Status { get; private set; }
    public string PreparedByUserId { get; private set; }
    public DateTime BatchedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? ReimbursedAtUtc { get; private set; }
    public string? CentralBankReference { get; private set; }

    #pragma warning disable CS8618
    protected GovernmentIncentiveClaimBatch() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public GovernmentIncentiveClaimBatch(
        Guid id,
        string batchNumber,
        string claimPeriod,
        int totalTransactionCount,
        Money totalClaimedIncentiveBdt,
        string preparedByUserId,
        DateTime batchedAtUtc) : base(id)
    {
        if (string.IsNullOrWhiteSpace(batchNumber))
            throw new ArgumentException("Batch number is required.", nameof(batchNumber));
        if (string.IsNullOrWhiteSpace(claimPeriod))
            throw new ArgumentException("Claim period is required.", nameof(claimPeriod));
        if (string.IsNullOrWhiteSpace(preparedByUserId))
            throw new ArgumentException("Prepared by user ID is required.", nameof(preparedByUserId));
        ArgumentNullException.ThrowIfNull(totalClaimedIncentiveBdt, nameof(totalClaimedIncentiveBdt));

        if (totalTransactionCount <= 0)
        {
            throw new InvariantViolationException(
                "EmptyIncentiveClaimBatchInvariant",
                "BRPD Circular 12/2024 (2.2.3)",
                "Government incentive claim batch must contain at least one eligible remittance transaction.");
        }

        if (totalClaimedIncentiveBdt.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidIncentiveClaimAmountInvariant",
                "BRPD Circular 12/2024 (2.2.3)",
                "Total claimed incentive amount must be strictly greater than zero.");
        }

        BatchNumber = batchNumber.Trim().ToUpperInvariant();
        ClaimPeriod = claimPeriod.Trim();
        TotalTransactionCount = totalTransactionCount;
        TotalClaimedIncentiveBdt = totalClaimedIncentiveBdt;
        PreparedByUserId = preparedByUserId.Trim();
        BatchedAtUtc = batchedAtUtc;
        Status = IncentiveClaimBatchStatus.Batched;
    }

    public void SubmitToBangladeshBank(string centralBankSubmissionRef, DateTime submittedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(centralBankSubmissionRef))
            throw new ArgumentException("Central Bank submission reference is required.", nameof(centralBankSubmissionRef));

        if (Status != IncentiveClaimBatchStatus.Batched)
        {
            throw new InvariantViolationException(
                "InvalidBatchStatusForSubmission",
                "BRPD Circular 12/2024 (2.2.3)",
                $"Only Batched claims can be submitted to Bangladesh Bank (current status: '{Status}').");
        }

        CentralBankReference = centralBankSubmissionRef.Trim().ToUpperInvariant();
        SubmittedAtUtc = submittedAtUtc;
        Status = IncentiveClaimBatchStatus.SubmittedToBangladeshBank;
    }

    public void MarkReimbursed(string centralBankSettlementRef, DateTime reimbursedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(centralBankSettlementRef))
            throw new ArgumentException("Central Bank settlement reference is required.", nameof(centralBankSettlementRef));

        if (Status != IncentiveClaimBatchStatus.SubmittedToBangladeshBank)
        {
            throw new InvariantViolationException(
                "InvalidBatchStatusForReimbursement",
                "BRPD Circular 12/2024 (2.2.3)",
                $"Only submitted claim batches can be marked as reimbursed by central bank (current status: '{Status}').");
        }

        CentralBankReference = centralBankSettlementRef.Trim().ToUpperInvariant();
        ReimbursedAtUtc = reimbursedAtUtc;
        Status = IncentiveClaimBatchStatus.Reimbursed;
    }
}
