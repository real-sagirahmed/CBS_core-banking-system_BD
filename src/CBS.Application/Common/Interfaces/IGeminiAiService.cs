using CBS.Application.AiIntelligence.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface IGeminiAiService
{
    Task<GeminiAdvisoryGeneratedResult> GenerateAdvisoryAsync(
        string sanitizedPrompt,
        IReadOnlyList<string> groundedContextChunks,
        CancellationToken cancellationToken = default);

    Task<GeminiTbmlEvaluationResult> EvaluateTbmlAsync(
        string goodsDescription,
        decimal declaredUnitPrice,
        decimal benchmarkUnitPrice,
        CancellationToken cancellationToken = default);
}
