using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Reconciliation;

// ARCHITECTURAL INTENT: Multi-Channel Unreconciled Transaction Aging & Statutory Suspense Provisioning
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 1.4.1.
public class ReconciliationAgingItem : AggregateRoot<Guid>
{
    public string ChannelType { get; private set; } // "InterBranch", "Nostro", "ATM", "RTGS", "BEFTN"
    public string TraceOrReferenceNumber { get; private set; }
    public string OriginatingBranchCode { get; private set; }
    public string RespondingBranchOrCounterparty { get; private set; }
    public Money TransactionAmount { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public DateOnly LastAssessmentDate { get; private set; }

    public int AgeInDays { get; private set; }
    public ReconciliationAgingBucket Bucket { get; private set; }
    public decimal RequiredProvisionPercentage { get; private set; }
    public Money RequiredProvisionAmount { get; private set; }

    public bool IsResolved { get; private set; }
    public DateOnly? ResolvedDate { get; private set; }
    public string? ResolutionReference { get; private set; }

    #pragma warning disable CS8618
    private ReconciliationAgingItem() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public ReconciliationAgingItem(
        Guid id,
        string channelType,
        string traceOrReferenceNumber,
        string originatingBranchCode,
        string respondingCounterparty,
        Money transactionAmount,
        DateOnly transactionDate,
        DateOnly assessmentDate) : base(id)
    {
        if (string.IsNullOrWhiteSpace(channelType)) throw new ArgumentException("Channel type is required.", nameof(channelType));
        if (string.IsNullOrWhiteSpace(traceOrReferenceNumber)) throw new ArgumentException("Trace/Reference number is required.", nameof(traceOrReferenceNumber));
        if (string.IsNullOrWhiteSpace(originatingBranchCode)) throw new ArgumentException("Originating branch is required.", nameof(originatingBranchCode));
        if (string.IsNullOrWhiteSpace(respondingCounterparty)) throw new ArgumentException("Responding counterparty is required.", nameof(respondingCounterparty));
        ArgumentNullException.ThrowIfNull(transactionAmount, nameof(transactionAmount));

        if (transactionAmount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidReconciliationAmountInvariant",
                "BRPD Circular 12/2024 (1.4.1)",
                "Unreconciled transaction amount must be strictly positive.");
        }

        ChannelType = channelType.Trim();
        TraceOrReferenceNumber = traceOrReferenceNumber.Trim();
        OriginatingBranchCode = originatingBranchCode.Trim();
        RespondingBranchOrCounterparty = respondingCounterparty.Trim();
        TransactionAmount = transactionAmount;
        TransactionDate = transactionDate;
        IsResolved = false;

        UpdateAging(assessmentDate);
    }

    /// <summary>
    /// Computes age, assigns statutory regulatory bucket, and calculates required provision (BRPD 1.4.1-ii, iv).
    /// </summary>
    public void UpdateAging(DateOnly currentAssessmentDate)
    {
        if (IsResolved)
            return;

        int age = Math.Max(0, currentAssessmentDate.DayNumber - TransactionDate.DayNumber);
        AgeInDays = age;
        LastAssessmentDate = currentAssessmentDate;

        (Bucket, RequiredProvisionPercentage) = age switch
        {
            <= 30 => (ReconciliationAgingBucket.Days0To30, 0.0m),
            <= 60 => (ReconciliationAgingBucket.Days31To60, 10.0m),
            <= 90 => (ReconciliationAgingBucket.Days61To90, 25.0m),
            <= 180 => (ReconciliationAgingBucket.Days91To180, 50.0m),
            _ => (ReconciliationAgingBucket.Over180Days, 100.0m)
        };

        decimal provAmt = Math.Round(TransactionAmount.Amount * (RequiredProvisionPercentage / 100m), 2, MidpointRounding.AwayFromZero);
        RequiredProvisionAmount = Money.Bdt(provAmt);
    }

    public void MarkResolved(string resolutionReference, DateOnly resolutionDate)
    {
        if (string.IsNullOrWhiteSpace(resolutionReference))
        {
            throw new ArgumentException("Resolution reference is required.", nameof(resolutionReference));
        }

        IsResolved = true;
        ResolvedDate = resolutionDate;
        ResolutionReference = resolutionReference.Trim();
        RequiredProvisionPercentage = 0m;
        RequiredProvisionAmount = Money.Zero(Currency.BDT);
    }
}
