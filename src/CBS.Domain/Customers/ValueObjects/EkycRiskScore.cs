using CBS.Domain.Enums;

namespace CBS.Domain.Customers.ValueObjects;

// ARCHITECTURAL INTENT: 8-Factor e-KYC composite risk score calculation
// in compliance with Bangladesh Bank e-KYC Guidelines (BRPD 8.1.13 & BFIU Master Circular 26).
public sealed record EkycRiskScore
{
    public int OnboardingChannelScore { get; init; }
    public int GeographicRiskScore { get; init; }
    public int PepRiskScore { get; init; }
    public int ProfessionSectorRiskScore { get; init; }
    public int ProductRiskScore { get; init; }
    public int TransactionalVolumeScore { get; init; }
    public int AdverseHistoryScore { get; init; }
    public int EntityLegalFormScore { get; init; }
    public int CompositeScore { get; init; }
    public CustomerRiskLevel RiskLevel { get; init; }
    public bool IsEddRequired { get; init; }

    #pragma warning disable CS8618
    public EkycRiskScore() { }
    #pragma warning restore CS8618

    [System.Text.Json.Serialization.JsonConstructor]
    public EkycRiskScore(
        int onboardingChannelScore,
        int geographicRiskScore,
        int pepRiskScore,
        int professionSectorRiskScore,
        int productRiskScore,
        int transactionalVolumeScore,
        int adverseHistoryScore,
        int entityLegalFormScore)
    {
        OnboardingChannelScore = onboardingChannelScore;
        GeographicRiskScore = geographicRiskScore;
        PepRiskScore = pepRiskScore;
        ProfessionSectorRiskScore = professionSectorRiskScore;
        ProductRiskScore = productRiskScore;
        TransactionalVolumeScore = transactionalVolumeScore;
        AdverseHistoryScore = adverseHistoryScore;
        EntityLegalFormScore = entityLegalFormScore;

        int rawTotal = onboardingChannelScore + geographicRiskScore + pepRiskScore +
                       professionSectorRiskScore + productRiskScore + transactionalVolumeScore +
                       adverseHistoryScore + entityLegalFormScore;

        // Normalize raw score (max potential ~300) to 1-100 scale
        CompositeScore = Math.Clamp((int)Math.Round(rawTotal / 3.0, MidpointRounding.AwayFromZero), 1, 100);

        if (CompositeScore > 60 || pepRiskScore >= 30 || adverseHistoryScore >= 40)
        {
            RiskLevel = CustomerRiskLevel.High;
            IsEddRequired = true;
        }
        else if (CompositeScore >= 35)
        {
            RiskLevel = CustomerRiskLevel.Medium;
            IsEddRequired = false;
        }
        else
        {
            RiskLevel = CustomerRiskLevel.Low;
            IsEddRequired = false;
        }
    }
}
