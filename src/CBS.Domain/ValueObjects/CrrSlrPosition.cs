using CBS.Domain.Enums;

namespace CBS.Domain.ValueObjects;

/// <summary>
/// Cash Reserve Ratio (CRR) and Statutory Liquidity Ratio (SLR) position evaluation
/// conforming to Bangladesh Bank DOS/MPD circulars and BRPD Circular 12 (Module 6: Treasury & ALM).
/// Invariants:
/// - CRR Daily Minimum: 3.50% of NDTL; Bi-weekly average: 4.00%.
/// - SLR Required: 13.00% for Conventional Banks; 5.50% for Islamic Banks.
/// </summary>
public sealed record CrrSlrPosition
{
    public Money Ndtl { get; }
    public BankingType BankingType { get; }
    public decimal CrrRequiredBiWeeklyRate { get; } = 4.00m;
    public decimal CrrDailyMinRate { get; } = 3.50m;
    public decimal SlrRequiredRate { get; }
    public Money MaintainedCrrCash { get; }
    public Money MaintainedSlrSecurities { get; }
    public decimal ActualCrrPercentage { get; }
    public decimal ActualSlrPercentage { get; }
    public bool HasCrrShortfall { get; }
    public bool HasSlrShortfall { get; }
    public Money CrrShortfallOrSurplusAmount { get; }
    public Money SlrShortfallOrSurplusAmount { get; }

    private CrrSlrPosition(
        Money ndtl,
        BankingType bankingType,
        decimal slrRequiredRate,
        Money maintainedCrrCash,
        Money maintainedSlrSecurities,
        decimal actualCrrPercentage,
        decimal actualSlrPercentage,
        bool hasCrrShortfall,
        bool hasSlrShortfall,
        Money crrShortfallOrSurplusAmount,
        Money slrShortfallOrSurplusAmount)
    {
        Ndtl = ndtl;
        BankingType = bankingType;
        SlrRequiredRate = slrRequiredRate;
        MaintainedCrrCash = maintainedCrrCash;
        MaintainedSlrSecurities = maintainedSlrSecurities;
        ActualCrrPercentage = actualCrrPercentage;
        ActualSlrPercentage = actualSlrPercentage;
        HasCrrShortfall = hasCrrShortfall;
        HasSlrShortfall = hasSlrShortfall;
        CrrShortfallOrSurplusAmount = crrShortfallOrSurplusAmount;
        SlrShortfallOrSurplusAmount = slrShortfallOrSurplusAmount;
    }

    public static CrrSlrPosition Calculate(
        Money ndtl,
        BankingType bankingType,
        Money maintainedCrrCash,
        Money maintainedSlrSecurities)
    {
        ArgumentNullException.ThrowIfNull(ndtl, nameof(ndtl));
        ArgumentNullException.ThrowIfNull(maintainedCrrCash, nameof(maintainedCrrCash));
        ArgumentNullException.ThrowIfNull(maintainedSlrSecurities, nameof(maintainedSlrSecurities));

        if (ndtl.Amount <= 0m)
            throw new ArgumentException("Net Demand and Time Liabilities (NDTL) must be greater than zero.", nameof(ndtl));

        // Statutory SLR: 13.00% Conventional, 5.50% Islamic
        decimal slrRate = bankingType == BankingType.Conventional ? 13.00m : 5.50m;
        decimal crrMinDailyRate = 3.50m;

        decimal requiredDailyCrrAmount = Math.Round(ndtl.Amount * (crrMinDailyRate / 100m), 2);
        decimal requiredSlrAmount = Math.Round(ndtl.Amount * (slrRate / 100m), 2);

        decimal actualCrrPct = Math.Round((maintainedCrrCash.Amount / ndtl.Amount) * 100m, 2);
        decimal actualSlrPct = Math.Round((maintainedSlrSecurities.Amount / ndtl.Amount) * 100m, 2);

        bool hasCrrShortfall = maintainedCrrCash.Amount < requiredDailyCrrAmount;
        bool hasSlrShortfall = maintainedSlrSecurities.Amount < requiredSlrAmount;

        decimal crrDiff = maintainedCrrCash.Amount - requiredDailyCrrAmount;
        decimal slrDiff = maintainedSlrSecurities.Amount - requiredSlrAmount;

        var crrDiffMoney = new Money(crrDiff, ndtl.Currency);
        var slrDiffMoney = new Money(slrDiff, ndtl.Currency);

        return new CrrSlrPosition(
            ndtl,
            bankingType,
            slrRate,
            maintainedCrrCash,
            maintainedSlrSecurities,
            actualCrrPct,
            actualSlrPct,
            hasCrrShortfall,
            hasSlrShortfall,
            crrDiffMoney,
            slrDiffMoney);
    }
}
