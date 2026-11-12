// ARCHITECTURAL INTENT: Repository contract for Borrower Internal Credit Risk Rating (ICRR).
// Conforms to Bangladesh Bank BRPD Circular No. 12 (Module 8.1.16) and BRPD Circular No. 03/2019.

using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface IBorrowerIcrrRepository
{
    Task<BorrowerIcrrAssessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BorrowerIcrrAssessment?> GetLatestByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BorrowerIcrrAssessment>> GetAllByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task AddAsync(BorrowerIcrrAssessment assessment, CancellationToken cancellationToken = default);
    Task UpdateAsync(BorrowerIcrrAssessment assessment, CancellationToken cancellationToken = default);
}
