// ARCHITECTURAL INTENT: Repository contract for Customer Risk Assessment (CRG).
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 8.1.13 and e-KYC Guidelines.

using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface ICustomerRiskAssessmentRepository
{
    Task<CustomerRiskAssessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerRiskAssessment?> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task AddAsync(CustomerRiskAssessment assessment, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomerRiskAssessment assessment, CancellationToken cancellationToken = default);
}
