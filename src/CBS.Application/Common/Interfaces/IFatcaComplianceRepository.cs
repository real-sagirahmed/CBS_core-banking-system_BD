// ARCHITECTURAL INTENT: Repository contract for FATCA Compliance Profiles.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 8.1.15 and US FATCA Regulations.

using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface IFatcaComplianceRepository
{
    Task<FatcaComplianceProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FatcaComplianceProfile?> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FatcaComplianceProfile>> GetRecalcitrantAccountsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(FatcaComplianceProfile profile, CancellationToken cancellationToken = default);
    Task UpdateAsync(FatcaComplianceProfile profile, CancellationToken cancellationToken = default);
}
