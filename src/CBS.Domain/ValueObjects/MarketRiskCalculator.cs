namespace CBS.Domain.ValueObjects;

public sealed record ParametricVaRResult(
    decimal PortfolioValue,
    decimal ConfidenceLevel,
    int HoldingDays,
    decimal AnnualVolatilityPercentage,
    decimal DailyVolatilityPercentage,
    decimal ValueAtRiskAmount,
    decimal ValueAtRiskPercentage);

public sealed record FxCurrencyPosition(
    string Currency,
    decimal LongExposure,
    decimal ShortExposure,
    decimal NetExposure);

public sealed record FxNetOpenPositionResult(
    IReadOnlyList<FxCurrencyPosition> Positions,
    decimal TotalNetLongExposure,
    decimal TotalNetShortExposure,
    decimal OverallNetOpenPosition,
    decimal ApprovedNopLimit,
    bool HasNopLimitBreach,
    decimal UtilizationPercentage);

/// <summary>
/// Market Risk Calculator conforming to Bangladesh Bank Risk Management Guidelines and BRPD Circular 12 Module 6.5.
/// Calculates 99% Parametric Value at Risk (VaR) and Overall Foreign Exchange Net Open Position (NOP).
/// </summary>
public static class MarketRiskCalculator
{
    private const decimal Z99 = 2.326347874m; // 99% one-tailed critical value
    private const decimal Z95 = 1.644853627m; // 95% one-tailed critical value
    private const decimal TradingDaysPerYear = 250m;

    /// <summary>
    /// Computes Parametric Value at Risk (VaR).
    /// Formula: VaR = PortfolioValue * Z * (AnnualVol / sqrt(250)) * sqrt(HoldingDays)
    /// </summary>
    public static ParametricVaRResult CalculateParametricVaR(
        decimal portfolioValue,
        decimal annualVolatilityPercentage,
        int holdingDays = 1,
        decimal confidenceLevel = 0.99m)
    {
        if (portfolioValue <= 0m)
            throw new ArgumentException("Portfolio value must be strictly positive.", nameof(portfolioValue));
        if (annualVolatilityPercentage <= 0m)
            throw new ArgumentException("Annual volatility percentage must be strictly positive.", nameof(annualVolatilityPercentage));
        if (holdingDays <= 0)
            throw new ArgumentException("Holding days must be positive.", nameof(holdingDays));

        decimal z = confidenceLevel >= 0.99m ? Z99 : Z95;
        double sqrtTradingDays = Math.Sqrt((double)TradingDaysPerYear);
        decimal dailyVolatility = (annualVolatilityPercentage / 100m) / (decimal)sqrtTradingDays;
        double sqrtHoldingDays = Math.Sqrt(holdingDays);

        decimal varFraction = z * dailyVolatility * (decimal)sqrtHoldingDays;
        decimal varAmount = Math.Round(portfolioValue * varFraction, 2);
        decimal varPct = Math.Round(varFraction * 100m, 4);

        return new ParametricVaRResult(
            portfolioValue,
            confidenceLevel,
            holdingDays,
            annualVolatilityPercentage,
            Math.Round(dailyVolatility * 100m, 4),
            varAmount,
            varPct);
    }

    /// <summary>
    /// Computes Foreign Exchange Net Open Position (NOP) conforming to Bangladesh Bank guidelines.
    /// Invariant: Overall NOP = max(Sum of Net Longs, Sum of Net Shorts).
    /// </summary>
    public static FxNetOpenPositionResult CalculateFxNetOpenPosition(
        IEnumerable<(string Currency, decimal LongExposure, decimal ShortExposure)> rawPositions,
        decimal approvedNopLimit)
    {
        if (approvedNopLimit <= 0m)
            throw new ArgumentException("Approved NOP limit must be strictly positive.", nameof(approvedNopLimit));

        var positions = new List<FxCurrencyPosition>();
        decimal sumLongs = 0m;
        decimal sumShorts = 0m;

        foreach (var p in rawPositions)
        {
            decimal net = p.LongExposure - p.ShortExposure;
            positions.Add(new FxCurrencyPosition(p.Currency.ToUpperInvariant(), p.LongExposure, p.ShortExposure, net));

            if (net > 0m)
            {
                sumLongs += net;
            }
            else if (net < 0m)
            {
                sumShorts += Math.Abs(net);
            }
        }

        decimal overallNop = Math.Max(sumLongs, sumShorts);
        bool hasBreach = overallNop > approvedNopLimit;
        decimal utilizationPct = Math.Round((overallNop / approvedNopLimit) * 100m, 2);

        return new FxNetOpenPositionResult(
            positions.AsReadOnly(),
            sumLongs,
            sumShorts,
            overallNop,
            approvedNopLimit,
            hasBreach,
            utilizationPct);
    }
}
