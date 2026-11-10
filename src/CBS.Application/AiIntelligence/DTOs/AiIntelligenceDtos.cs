using CBS.Domain.Enums;

namespace CBS.Application.AiIntelligence.DTOs;

public sealed record AskAiAdvisoryRequest(
    string QueryId,
    string OfficerUserId,
    UserRole Role,
    string BranchCode,
    AiQueryCategory Category,
    string RawQueryText);

public sealed record GroundedCitationDto(
    string CircularReference,
    string SectionTitle,
    string RelevantExcerpt,
    decimal RelevanceScore);

public sealed record AiAdvisoryResponse(
    string QueryId,
    string OfficerUserId,
    string Category,
    string SanitizedQueryText,
    string AdvisoryResponse,
    IReadOnlyList<GroundedCitationDto> Citations,
    decimal ConfidenceScore,
    bool RequiresMakerCheckerReview,
    DateTime CreatedAtUtc);

public sealed record AnalyzeTbmlRequest(
    string AssessmentId,
    string LcReference,
    string GoodsDescription,
    decimal DeclaredUnitPrice,
    decimal BenchmarkUnitPrice,
    bool DualUseRiskIdentified,
    bool SanctionedRouteRiskIdentified,
    string Rationale,
    string OfficerUserId);

public sealed record TbmlAssessmentResponse(
    string AssessmentId,
    string LcReference,
    string GoodsDescription,
    decimal DeclaredUnitPrice,
    decimal BenchmarkUnitPrice,
    decimal UnitPriceVariancePercentage,
    bool DualUseRiskIdentified,
    bool SanctionedRouteRiskIdentified,
    string Recommendation,
    decimal AnomalyRiskScore,
    string Rationale,
    bool RequiresMakerCheckerReview,
    DateTime EvaluatedAtUtc);

public sealed record GeminiAdvisoryGeneratedResult(
    string ResponseText,
    decimal ConfidenceScore);

public sealed record GeminiTbmlEvaluationResult(
    string Rationale,
    decimal RiskScore);

public sealed record PiiSanitizeRequest(
    string RawText);

public sealed record PiiSanitizeResponse(
    string SanitizedText,
    bool HadRawPii);

public sealed record RegulatoryKnowledgeDocumentDto(
    string DocumentId,
    string CircularNo,
    string ModuleReference,
    string Title,
    string Content,
    IReadOnlyList<string> Tags);

public sealed record SearchKnowledgeBaseRequest(
    string Query,
    int TopK = 3);

