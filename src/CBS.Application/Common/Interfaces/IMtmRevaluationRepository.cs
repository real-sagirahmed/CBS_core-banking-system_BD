// ARCHITECTURAL INTENT: Repository contract for MTM Revaluation batches.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) and DOS Circular 05/2008.

using CBS.Domain.Treasury;

namespace CBS.Application.Common.Interfaces;

public interface IMtmRevaluationRepository
{
    Task<MtmRevaluationBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MtmRevaluationBatch>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(MtmRevaluationBatch batch, CancellationToken cancellationToken = default);
}
