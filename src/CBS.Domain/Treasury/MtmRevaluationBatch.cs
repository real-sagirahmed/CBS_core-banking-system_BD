// ARCHITECTURAL INTENT: Mark-to-Market (MTM) Revaluation Batch Aggregate Root
// Executing daily MTM revaluation for FX Forward contracts and Government Securities (HFT)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 6.3.1-iii, 6.3.1-v, 6.5.7, and DOS Circular 05/2008.

using CBS.Domain.Common;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Treasury;

public sealed class MtmRevaluationBatch : AggregateRoot<Guid>
{
    public string BatchReference { get; private set; } = null!;
    public DateOnly RevaluationDate { get; private set; }
    public int TotalForwardDealsEvaluated { get; private set; }
    public int TotalSecuritiesEvaluated { get; private set; }
    public decimal TotalUnrealizedGainsBdt { get; private set; }
    public decimal TotalUnrealizedLossesBdt { get; private set; }
    public decimal NetRevaluationReserveBdt => TotalUnrealizedGainsBdt - TotalUnrealizedLossesBdt;
    public string ExecutedByUserId { get; private set; } = null!;
    public DateTime ExecutedAtUtc { get; private set; }
    public string Status { get; private set; } = null!;
    public string? Remarks { get; private set; }

    #pragma warning disable CS8618
    private MtmRevaluationBatch() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public MtmRevaluationBatch(
        Guid id,
        string batchReference,
        DateOnly revaluationDate,
        int totalForwardDealsEvaluated,
        int totalSecuritiesEvaluated,
        decimal totalUnrealizedGainsBdt,
        decimal totalUnrealizedLossesBdt,
        string executedByUserId,
        string? remarks = null)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(batchReference))
            throw new ArgumentException("Batch reference is required.", nameof(batchReference));
        if (string.IsNullOrWhiteSpace(executedByUserId))
            throw new ArgumentException("Executed by user ID is required.", nameof(executedByUserId));

        BatchReference = batchReference.Trim().ToUpperInvariant();
        RevaluationDate = revaluationDate;
        TotalForwardDealsEvaluated = totalForwardDealsEvaluated;
        TotalSecuritiesEvaluated = totalSecuritiesEvaluated;
        TotalUnrealizedGainsBdt = Math.Max(0m, totalUnrealizedGainsBdt);
        TotalUnrealizedLossesBdt = Math.Max(0m, totalUnrealizedLossesBdt);
        ExecutedByUserId = executedByUserId.Trim();
        ExecutedAtUtc = DateTime.UtcNow;
        Status = "Completed";
        Remarks = remarks?.Trim();
    }
}
