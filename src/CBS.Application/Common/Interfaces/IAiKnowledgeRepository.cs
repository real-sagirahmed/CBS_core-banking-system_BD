using CBS.Domain.AiIntelligence;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface IAiKnowledgeRepository
{
    Task<IReadOnlyList<RegulatoryKnowledgeDocument>> SearchKnowledgeBaseAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegulatoryKnowledgeDocument>> GetAllKnowledgeDocumentsAsync(
        CancellationToken cancellationToken = default);

    Task SaveAdvisoryQueryAsync(
        AiAdvisoryQuery query,
        CancellationToken cancellationToken = default);

    Task<AiAdvisoryQuery?> GetAdvisoryQueryByIdAsync(
        string queryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiAdvisoryQuery>> GetAllAdvisoryQueriesAsync(
        CancellationToken cancellationToken = default);

    Task SaveTbmlAssessmentAsync(
        TbmlSemanticAssessment assessment,
        CancellationToken cancellationToken = default);

    Task<TbmlSemanticAssessment?> GetTbmlAssessmentByIdAsync(
        string assessmentId,
        CancellationToken cancellationToken = default);
}
