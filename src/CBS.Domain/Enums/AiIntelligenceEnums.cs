namespace CBS.Domain.Enums;

public enum AiQueryCategory
{
    RegulatoryCompliance = 1,
    TbmlRiskScreening = 2,
    AlcoLiquidityAnalysis = 3,
    GeneralBankingPolicy = 4
}

public enum TbmlAiRecommendation
{
    LowRiskProceed = 1,
    RequireHumanReview = 2,
    EscalateToBfiu = 3,
    RejectTransaction = 4
}
