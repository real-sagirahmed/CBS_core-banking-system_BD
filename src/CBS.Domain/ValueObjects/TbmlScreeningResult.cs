using CBS.Domain.Enums;

namespace CBS.Domain.ValueObjects;

/// <summary>
/// Result of Trade-Based Money Laundering (TBML) and Sanction Pre-Issuance Screening.
/// Reference: BRPD Circular 12, 5.0, 5.7.1 - 5.7.8.
/// </summary>
public sealed record TbmlScreeningResult
{
    public bool HasSanctionHit { get; }
    public bool HasPriceVarianceAnomaly { get; }
    public bool HasHighRiskPortRoute { get; }
    public bool HasDualUseGoodsRisk { get; }
    public decimal UnitPriceVariancePercentage { get; }
    public TbmlRiskRating RiskRating { get; }
    public string RiskSummary { get; }

    private TbmlScreeningResult(
        bool hasSanctionHit,
        bool hasPriceVarianceAnomaly,
        bool hasHighRiskPortRoute,
        bool hasDualUseGoodsRisk,
        decimal unitPriceVariancePercentage,
        TbmlRiskRating riskRating,
        string riskSummary)
    {
        HasSanctionHit = hasSanctionHit;
        HasPriceVarianceAnomaly = hasPriceVarianceAnomaly;
        HasHighRiskPortRoute = hasHighRiskPortRoute;
        HasDualUseGoodsRisk = hasDualUseGoodsRisk;
        UnitPriceVariancePercentage = unitPriceVariancePercentage;
        RiskRating = riskRating;
        RiskSummary = riskSummary;
    }

    public static TbmlScreeningResult Evaluate(
        bool entitySanctionHit,
        decimal declaredUnitPrice,
        decimal benchmarkMinPrice,
        decimal benchmarkMaxPrice,
        string portOfLoading,
        string portOfDischarge,
        bool isDualUseHsCode)
    {
        var reasons = new List<string>();
        var hasSanctionHit = entitySanctionHit;
        if (hasSanctionHit)
        {
            reasons.Add("UN/OFAC/Local sanction list hit detected.");
        }

        // Price anomaly check: compare against benchmark band
        var hasPriceAnomaly = false;
        decimal variancePct = 0m;

        if (benchmarkMaxPrice > 0 && benchmarkMinPrice > 0)
        {
            if (declaredUnitPrice > benchmarkMaxPrice)
            {
                variancePct = Math.Round(((declaredUnitPrice - benchmarkMaxPrice) / benchmarkMaxPrice) * 100m, 2);
                if (variancePct > 15.0m) // Over-invoicing alert
                {
                    hasPriceAnomaly = true;
                    reasons.Add($"Over-invoicing suspected: unit price {declaredUnitPrice} exceeds benchmark ceiling {benchmarkMaxPrice} by {variancePct}%.");
                }
            }
            else if (declaredUnitPrice < benchmarkMinPrice)
            {
                variancePct = Math.Round(((benchmarkMinPrice - declaredUnitPrice) / benchmarkMinPrice) * -100m, 2);
                if (variancePct < -15.0m) // Under-invoicing alert
                {
                    hasPriceAnomaly = true;
                    reasons.Add($"Under-invoicing suspected: unit price {declaredUnitPrice} is below benchmark floor {benchmarkMinPrice} by {Math.Abs(variancePct)}%.");
                }
            }
        }

        // Sanctioned or high risk port check (e.g. Bandar Abbas, Tartus, Sevastopol)
        var highRiskPorts = new[] { "BANDAR ABBAS", "TARTUS", "SEVASTOPOL", "LATAKIA", "CRIMEA" };
        var hasHighRiskPort = highRiskPorts.Any(p =>
            portOfLoading.Contains(p, StringComparison.OrdinalIgnoreCase) ||
            portOfDischarge.Contains(p, StringComparison.OrdinalIgnoreCase));

        if (hasHighRiskPort)
        {
            reasons.Add("High-risk or sanctioned port routing detected in trade itinerary.");
        }

        if (isDualUseHsCode)
        {
            reasons.Add("Dual-use military/civilian goods HS Code identified; requires prior regulatory clearance.");
        }

        // Determine Risk Rating
        TbmlRiskRating rating;
        if (hasSanctionHit)
        {
            rating = TbmlRiskRating.ExtremeHit;
        }
        else if (hasPriceAnomaly || hasHighRiskPort || isDualUseHsCode)
        {
            rating = (hasPriceAnomaly && hasHighRiskPort) ? TbmlRiskRating.High : TbmlRiskRating.Medium;
        }
        else
        {
            rating = TbmlRiskRating.Low;
        }

        var summary = reasons.Count > 0 ? string.Join("; ", reasons) : "Clean TBML and Sanction Screening.";
        return new TbmlScreeningResult(hasSanctionHit, hasPriceAnomaly, hasHighRiskPort, isDualUseHsCode, variancePct, rating, summary);
    }
}
