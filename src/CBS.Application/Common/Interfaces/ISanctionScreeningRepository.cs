// ARCHITECTURAL INTENT: Repository contract for Sanction Screening Records.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 8.1.14 and AML Act 2012.

using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface ISanctionScreeningRepository
{
    Task<SanctionScreeningRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SanctionScreeningRecord>> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SanctionScreeningRecord>> GetPendingInvestigationsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(SanctionScreeningRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(SanctionScreeningRecord record, CancellationToken cancellationToken = default);
}
