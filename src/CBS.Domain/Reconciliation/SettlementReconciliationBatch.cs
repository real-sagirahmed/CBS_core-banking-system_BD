using CBS.Domain.Common;
using CBS.Domain.Enums;

namespace CBS.Domain.Reconciliation;

/// <summary>
/// Value Object representing an unmatched item identified during 3-Way Reconciliation.
/// Categorized into aging buckets in accordance with BRPD Circular 12 Clause 1.4.1-ii.
/// </summary>
public sealed record UnmatchedReconItem(
    string ItemId,
    string Stan,
    string Rrn,
    string AccountNumber,
    decimal Amount,
    string DiscrepancyType,
    ReconAgingBucket AgingBucket,
    int DaysUnreconciled,
    bool IsRectified);

/// <summary>
/// Aggregate Root representing an Automated 3-Way Settlement Reconciliation Batch.
/// Cross-matches CBS Core Book vs. ADC Switch vs. National Payment Switch (NPSB/VISA).
/// Mandated by Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Clause 1.4.1.
/// </summary>
public sealed class SettlementReconciliationBatch : AggregateRoot<string>
{
    // ARCHITECTURAL INTENT: Automated 3-Way Reconciliation & Mismatched Aging Analysis (BRPD 1.4.1).
    // Automatically matches Core Ledgers, Switch Logs, and External Network settlement files.
    // Categorizes unresolved discrepancies into aging buckets (1-7d, 8-15d, 16-30d, >30d).

    public string BatchId => Id;
    public DateOnly ReconciliationDate { get; private set; }
    public AdcChannelType Channel { get; private set; }
    public int TotalCoreTransactions { get; private set; }
    public int TotalSwitchTransactions { get; private set; }
    public int TotalNetworkTransactions { get; private set; }
    public int MatchedCount { get; private set; }
    public int MismatchedCount { get; private set; }
    public ReconciliationStatus Status { get; private set; }
    public IReadOnlyList<UnmatchedReconItem> UnmatchedItems { get; private set; } = new List<UnmatchedReconItem>();
    public DateTime ExecutedAtUtc { get; private set; }

    // Parameterless constructor for EF Core ORM materialization
    private SettlementReconciliationBatch() : base(string.Empty) { }

    private SettlementReconciliationBatch(string batchId) : base(batchId) { }

    public static SettlementReconciliationBatch Create(
        string batchId,
        DateOnly reconciliationDate,
        AdcChannelType channel,
        int totalCore,
        int totalSwitch,
        int totalNetwork,
        int matchedCount,
        IEnumerable<UnmatchedReconItem> unmatchedItems)
    {
        if (string.IsNullOrWhiteSpace(batchId))
            throw new ArgumentException("Batch ID is required.", nameof(batchId));

        var itemsList = unmatchedItems.ToList();
        var status = itemsList.Count == 0
            ? ReconciliationStatus.Matched
            : ReconciliationStatus.DiscrepancyDetected;

        return new SettlementReconciliationBatch(batchId.Trim())
        {
            ReconciliationDate = reconciliationDate,
            Channel = channel,
            TotalCoreTransactions = totalCore,
            TotalSwitchTransactions = totalSwitch,
            TotalNetworkTransactions = totalNetwork,
            MatchedCount = matchedCount,
            MismatchedCount = itemsList.Count,
            Status = status,
            UnmatchedItems = itemsList.AsReadOnly(),
            ExecutedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkSettled()
    {
        Status = ReconciliationStatus.Settled;
    }
}
