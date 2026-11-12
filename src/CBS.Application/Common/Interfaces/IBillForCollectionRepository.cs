// ARCHITECTURAL INTENT: Repository interface for Outward/Inward Bills for Collection (OBC/IBC)
// per BRPD Circular No. 12 Module 2 Sections 2.2.4 & 2.2.5.

using CBS.Domain.Clearing;
using CBS.Domain.Enums;

namespace CBS.Application.Common.Interfaces;

public interface IBillForCollectionRepository
{
    Task<BillForCollection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BillForCollection?> GetByBillNumberAsync(string billNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillForCollection>> GetByCustomerAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillForCollection>> GetByStatusAsync(BillCollectionStatus status, CancellationToken cancellationToken = default);
    Task AddAsync(BillForCollection bill, CancellationToken cancellationToken = default);
    Task UpdateAsync(BillForCollection bill, CancellationToken cancellationToken = default);
}
