// ARCHITECTURAL INTENT: Statutory Reserve Assessment Aggregate Root
// Monitoring Cash Reserve Ratio (CRR) and Statutory Liquidity Ratio (SLR) against Total Demand and Time Liabilities (TDTL)
// per Bangladesh Bank MPD Circular No. 03/2020 and BRPD Circular No. 12 (13 May 2024) Module 6.3.1-l.

using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Treasury;

public sealed class StatutoryReserveAssessment : AggregateRoot<Guid>
{
    public DateOnly AssessmentDate { get; private set; }
    public BankingType BankingType { get; private set; }
    public decimal TdtlBaseBdt { get; private set; }
    public decimal RequiredCrrDailyBdt { get; private set; }
    public decimal RequiredCrrBiWeeklyBdt { get; private set; }
    public decimal MaintainedCrrCashBdt { get; private set; }
    public decimal DailyCrrShortfallBdt => Math.Max(0m, RequiredCrrDailyBdt - MaintainedCrrCashBdt);
    public decimal BiWeeklyCrrShortfallBdt => Math.Max(0m, RequiredCrrBiWeeklyBdt - MaintainedCrrCashBdt);
    public bool IsDailyCrrCompliant => DailyCrrShortfallBdt == 0m;

    public decimal RequiredSlrBdt { get; private set; }
    public decimal MaintainedSlrAssetsBdt { get; private set; }
    public decimal SlrShortfallBdt => Math.Max(0m, RequiredSlrBdt - MaintainedSlrAssetsBdt);
    public bool IsSlrCompliant => SlrShortfallBdt == 0m;

    public string EvaluatedByUserId { get; private set; } = null!;
    public DateTime EvaluatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private StatutoryReserveAssessment() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public StatutoryReserveAssessment(
        Guid id,
        DateOnly assessmentDate,
        BankingType bankingType,
        decimal tdtlBaseBdt,
        decimal maintainedCrrCashBdt,
        decimal maintainedSlrAssetsBdt,
        string evaluatedByUserId)
        : base(id)
    {
        if (tdtlBaseBdt <= 0)
            throw new ArgumentException("TDTL base amount must be strictly positive.", nameof(tdtlBaseBdt));
        if (string.IsNullOrWhiteSpace(evaluatedByUserId))
            throw new ArgumentException("Evaluated by user ID is required.", nameof(evaluatedByUserId));

        AssessmentDate = assessmentDate;
        BankingType = bankingType;
        TdtlBaseBdt = tdtlBaseBdt;
        MaintainedCrrCashBdt = Math.Max(0m, maintainedCrrCashBdt);
        MaintainedSlrAssetsBdt = Math.Max(0m, maintainedSlrAssetsBdt);
        EvaluatedByUserId = evaluatedByUserId.Trim();
        EvaluatedAtUtc = DateTime.UtcNow;

        // MPD Circular 03/2020 Ratios:
        // CRR: Daily minimum 3.50%, Bi-weekly average 4.00%
        RequiredCrrDailyBdt = Math.Round(tdtlBaseBdt * 0.035m, 2, MidpointRounding.AwayFromZero);
        RequiredCrrBiWeeklyBdt = Math.Round(tdtlBaseBdt * 0.040m, 2, MidpointRounding.AwayFromZero);

        // SLR: Conventional 13.00%, Islamic 5.50%
        decimal slrRate = bankingType == BankingType.Islamic ? 0.055m : 0.130m;
        RequiredSlrBdt = Math.Round(tdtlBaseBdt * slrRate, 2, MidpointRounding.AwayFromZero);
    }
}
