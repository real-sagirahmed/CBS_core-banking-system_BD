using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface ICustomerDocumentRecordRepository
{
    Task AddAsync(CustomerDocumentRecord document, CancellationToken cancellationToken = default);
    Task<CustomerDocumentRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerDocumentRecord>> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerDocumentRecord>> GetExpiredComplianceDocumentsAsync(DateOnly asOfDate, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomerDocumentRecord document, CancellationToken cancellationToken = default);
}
