using CBS.Application.AiIntelligence.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.AiIntelligence;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Application.AiIntelligence.Commands;

public sealed record AskAiAdvisoryCommand(AskAiAdvisoryRequest Request);
public sealed record AnalyzeTbmlCommand(AnalyzeTbmlRequest Request);
public sealed record PiiSanitizeCommand(PiiSanitizeRequest Request);

public class AiIntelligenceCommandHandler
{
    private readonly IAiKnowledgeRepository _aiRepository;
    private readonly IGeminiAiService _geminiAiService;
    private readonly ISecurityAuditRepository _auditRepository;

    public AiIntelligenceCommandHandler(
        IAiKnowledgeRepository aiRepository,
        IGeminiAiService geminiAiService,
        ISecurityAuditRepository auditRepository)
    {
        _aiRepository = aiRepository;
        _geminiAiService = geminiAiService;
        _auditRepository = auditRepository;
    }

    public async Task<AiAdvisoryResponse> HandleAskAdvisoryAsync(AskAiAdvisoryCommand command, CancellationToken ct = default)
    {
        var req = command.Request;

        // Invariant 1: Assert Zero Financial Mutation Lock (BRPD 1.3.5, 10.3.5-vii)
        AiAdvisoryQuery.AssertZeroFinancialMutation(req.RawQueryText);

        // Invariant 2: Client-side Deterministic PII Redaction (BRPD 9.1.1-xxv, 10.1.3)
        var sanitized = PiiSanitizer.SanitizePrompt(req.RawQueryText);

        // 3. Grounded Knowledge Base Hybrid Retrieval
        var retrievedDocs = await _aiRepository.SearchKnowledgeBaseAsync(sanitized, topK: 3, ct);
        var citations = retrievedDocs.Select((doc, idx) => new GroundedCitation(
            CircularReference: $"{doc.CircularNo} ({doc.ModuleReference})",
            SectionTitle: doc.Title,
            RelevantExcerpt: doc.Content,
            RelevanceScore: Math.Round(0.95m - (idx * 0.10m), 2)
        )).ToList();

        // 4. Invoke Gemini AI Advisory Service with grounded context
        var contextChunks = retrievedDocs.Select(d => $"[{d.ModuleReference}] {d.Title}: {d.Content}").ToList();
        var generated = await _geminiAiService.GenerateAdvisoryAsync(sanitized, contextChunks, ct);

        // 5. Create Domain Aggregate Root
        var query = AiAdvisoryQuery.Create(
            queryId: req.QueryId,
            officerUserId: req.OfficerUserId,
            role: req.Role,
            branchCode: req.BranchCode,
            category: req.Category,
            rawQueryText: req.RawQueryText,
            advisoryResponse: generated.ResponseText,
            citations: citations,
            confidenceScore: generated.ConfidenceScore,
            requiresMakerCheckerReview: true);

        await _aiRepository.SaveAdvisoryQueryAsync(query, ct);

        // 6. BRPD 10.3.6: Immutable CAATs Audit Logging for AI Model Invocations
        var latestSeq = await _auditRepository.GetLatestAuditSequenceAsync(ct);
        var latestHash = await _auditRepository.GetLatestAuditHashAsync(ct);

        var auditEntry = AuditJournalEntry.Create(
            sequenceNumber: latestSeq + 1,
            userId: req.OfficerUserId,
            role: req.Role,
            branchCode: req.BranchCode,
            eventCategory: AuditEventCategory.SecurityOverride,
            action: "GeminiAiAdvisoryConsulted",
            targetEntityName: "AiAdvisoryQuery",
            targetEntityId: query.QueryId,
            beforeStateJson: null,
            afterStateJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                query.QueryId,
                query.Category,
                CitationsCount = citations.Count,
                query.ConfidenceScore,
                query.RequiresMakerCheckerReview
            }),
            terminalIp: "127.0.0.1",
            previousEntryHash: latestHash);

        await _auditRepository.AddAuditEntryAsync(auditEntry, ct);

        return new AiAdvisoryResponse(
            query.QueryId,
            query.OfficerUserId,
            query.Category.ToString(),
            query.SanitizedQueryText,
            query.AdvisoryResponse,
            citations.Select(c => new GroundedCitationDto(c.CircularReference, c.SectionTitle, c.RelevantExcerpt, c.RelevanceScore)).ToList(),
            query.ConfidenceScore,
            query.RequiresMakerCheckerReview,
            query.CreatedAtUtc);
    }

    public async Task<TbmlAssessmentResponse> HandleAnalyzeTbmlAsync(AnalyzeTbmlCommand command, CancellationToken ct = default)
    {
        var req = command.Request;

        // Evaluate TBML semantic assessment
        var assessment = new TbmlSemanticAssessment(
            assessmentId: req.AssessmentId,
            lcReference: req.LcReference,
            goodsDescription: req.GoodsDescription,
            declaredUnitPrice: req.DeclaredUnitPrice,
            benchmarkUnitPrice: req.BenchmarkUnitPrice,
            dualUseRiskIdentified: req.DualUseRiskIdentified,
            sanctionedRouteRiskIdentified: req.SanctionedRouteRiskIdentified,
            rationale: req.Rationale);

        await _aiRepository.SaveTbmlAssessmentAsync(assessment, ct);

        // BRPD 10.3.6: Immutable CAATs Audit Logging
        var latestSeq = await _auditRepository.GetLatestAuditSequenceAsync(ct);
        var latestHash = await _auditRepository.GetLatestAuditHashAsync(ct);

        var auditEntry = AuditJournalEntry.Create(
            sequenceNumber: latestSeq + 1,
            userId: req.OfficerUserId,
            role: UserRole.ComplianceAuditor,
            branchCode: "0000",
            eventCategory: AuditEventCategory.SecurityOverride,
            action: "TbmlAiSemanticScreeningCompleted",
            targetEntityName: "TbmlSemanticAssessment",
            targetEntityId: assessment.AssessmentId,
            beforeStateJson: null,
            afterStateJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                assessment.AssessmentId,
                assessment.LcReference,
                assessment.UnitPriceVariancePercentage,
                assessment.Recommendation,
                assessment.AnomalyRiskScore
            }),
            terminalIp: "127.0.0.1",
            previousEntryHash: latestHash);

        await _auditRepository.AddAuditEntryAsync(auditEntry, ct);

        return new TbmlAssessmentResponse(
            assessment.AssessmentId,
            assessment.LcReference,
            assessment.GoodsDescription,
            assessment.DeclaredUnitPrice,
            assessment.BenchmarkUnitPrice,
            assessment.UnitPriceVariancePercentage,
            assessment.DualUseRiskIdentified,
            assessment.SanctionedRouteRiskIdentified,
            assessment.Recommendation.ToString(),
            assessment.AnomalyRiskScore,
            assessment.Rationale,
            assessment.RequiresMakerCheckerReview,
            assessment.EvaluatedAtUtc);
    }

    public Task<PiiSanitizeResponse> HandlePiiSanitizeAsync(PiiSanitizeCommand command)
    {
        var raw = command.Request.RawText;
        bool hadRawPii = PiiSanitizer.ContainsRawPii(raw);
        string sanitized = PiiSanitizer.SanitizePrompt(raw);
        return Task.FromResult(new PiiSanitizeResponse(sanitized, hadRawPii));
    }
}
