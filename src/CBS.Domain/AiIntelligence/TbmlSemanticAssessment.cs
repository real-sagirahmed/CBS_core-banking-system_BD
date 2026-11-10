using CBS.Domain.Enums;

namespace CBS.Domain.AiIntelligence;

/// <summary>
/// Domain model for TBML (Trade-Based Money Laundering) AI Semantic Assessment.
/// Enforces mandatory 4-Eyes Segregation (Maker-Checker) under BRPD 1.3.18, 5.4.1, 10.3.5-x.
/// </summary>
public sealed record TbmlSemanticAssessment
{
    // ARCHITECTURAL INTENT: Mandatory 4-Eyes (Maker-Checker) Lock for AI Recommendations (BRPD 1.3.18, 10.3.5-x).
    // An AI assessment cannot automatically approve or reject a commercial trade deal.
    // Every AI finding must require human Maker-Checker sign-off prior to operational action.

    public string AssessmentId { get; init; }
    public string LcReference { get; init; }
    public string GoodsDescription { get; init; }
    public decimal DeclaredUnitPrice { get; init; }
    public decimal BenchmarkUnitPrice { get; init; }
    public decimal UnitPriceVariancePercentage { get; init; }
    public bool DualUseRiskIdentified { get; init; }
    public bool SanctionedRouteRiskIdentified { get; init; }
    public TbmlAiRecommendation Recommendation { get; init; }
    public decimal AnomalyRiskScore { get; init; }
    public string Rationale { get; init; }
    public bool RequiresMakerCheckerReview { get; init; } = true;
    public DateTime EvaluatedAtUtc { get; init; }

    // Parameterless constructor for EF Core ORM materialization
    private TbmlSemanticAssessment()
    {
        AssessmentId = string.Empty;
        LcReference = string.Empty;
        GoodsDescription = string.Empty;
        Rationale = string.Empty;
        EvaluatedAtUtc = DateTime.UtcNow;
    }

    public TbmlSemanticAssessment(
        string assessmentId,
        string lcReference,
        string goodsDescription,
        decimal declaredUnitPrice,
        decimal benchmarkUnitPrice,
        bool dualUseRiskIdentified,
        bool sanctionedRouteRiskIdentified,
        string rationale)
    {
        if (string.IsNullOrWhiteSpace(assessmentId))
            throw new ArgumentException("Assessment ID is required.", nameof(assessmentId));
        if (string.IsNullOrWhiteSpace(lcReference))
            throw new ArgumentException("LC Reference is required.", nameof(lcReference));
        if (declaredUnitPrice <= 0)
            throw new ArgumentException("Declared unit price must be positive.", nameof(declaredUnitPrice));
        if (benchmarkUnitPrice <= 0)
            throw new ArgumentException("Benchmark unit price must be positive.", nameof(benchmarkUnitPrice));

        AssessmentId = assessmentId.Trim();
        LcReference = lcReference.Trim();
        GoodsDescription = goodsDescription.Trim();
        DeclaredUnitPrice = declaredUnitPrice;
        BenchmarkUnitPrice = benchmarkUnitPrice;
        DualUseRiskIdentified = dualUseRiskIdentified;
        SanctionedRouteRiskIdentified = sanctionedRouteRiskIdentified;
        Rationale = rationale.Trim();
        EvaluatedAtUtc = DateTime.UtcNow;

        // Calculate variance: ((Declared - Benchmark) / Benchmark) * 100
        UnitPriceVariancePercentage = Math.Round(((declaredUnitPrice - benchmarkUnitPrice) / benchmarkUnitPrice) * 100m, 2);

        // Determine Recommendation & Anomaly Risk Score
        decimal varianceAbs = Math.Abs(UnitPriceVariancePercentage);

        if (sanctionedRouteRiskIdentified)
        {
            Recommendation = TbmlAiRecommendation.RejectTransaction;
            AnomalyRiskScore = 0.95m;
        }
        else if (dualUseRiskIdentified || varianceAbs > 25.0m)
        {
            Recommendation = TbmlAiRecommendation.EscalateToBfiu;
            AnomalyRiskScore = 0.85m;
        }
        else if (varianceAbs > 15.0m)
        {
            // BRPD 5.4.1: Unit price anomaly variance band (+/- 15%)
            Recommendation = TbmlAiRecommendation.RequireHumanReview;
            AnomalyRiskScore = 0.60m;
        }
        else
        {
            Recommendation = TbmlAiRecommendation.LowRiskProceed;
            AnomalyRiskScore = 0.15m;
        }

        // BRPD 1.3.18, 10.3.5-x: Mandatory 4-Eyes lock
        RequiresMakerCheckerReview = true;
    }
}
