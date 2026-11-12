// ARCHITECTURAL INTENT: Repository contract for Statutory Reserve (CRR/SLR) assessments.
// Conforms to Bangladesh Bank MPD Circular No. 03/2020 and BRPD Circular No. 12 (13 May 2024).

using CBS.Domain.Treasury;

namespace CBS.Application.Common.Interfaces;

public interface IStatutoryReserveRepository
{
    Task<StatutoryReserveAssessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StatutoryReserveAssessment?> GetLatestAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatutoryReserveAssessment>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(StatutoryReserveAssessment assessment, CancellationToken cancellationToken = default);
}
