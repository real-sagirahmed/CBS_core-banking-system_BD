// ARCHITECTURAL INTENT: Repository interface for Inland Bills Purchased (IBP) and Discounting
// per BRPD Circular No. 12 Modules 2 & 4.

using CBS.Domain.Clearing;
using CBS.Domain.Enums;

namespace CBS.Application.Common.Interfaces;

public interface IInlandBillPurchasedRepository
{
    Task<InlandBillPurchased?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InlandBillPurchased?> GetByReferenceNumberAsync(string referenceNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InlandBillPurchased>> GetByCustomerAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InlandBillPurchased>> GetByStatusAsync(InlandBillPurchasedStatus status, CancellationToken cancellationToken = default);
    Task AddAsync(InlandBillPurchased ibp, CancellationToken cancellationToken = default);
    Task UpdateAsync(InlandBillPurchased ibp, CancellationToken cancellationToken = default);
}
