// ARCHITECTURAL INTENT: Repository interface for BACPS Cheque Dishonour Records
// and Section 138 Legal Notice tracking per BRPD Circular No. 12 Section 1.5.3.

using CBS.Domain.Clearing;

namespace CBS.Application.Common.Interfaces;

public interface IChequeDishonourRepository
{
    Task<ChequeDishonourRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ChequeDishonourRecord?> GetByMemoReferenceAsync(string memoReference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChequeDishonourRecord>> GetByDrawerAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChequeDishonourRecord>> GetByDrawerCifAsync(string cif, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChequeDishonourRecord>> GetPendingSection138NoticesAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ChequeDishonourRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(ChequeDishonourRecord record, CancellationToken cancellationToken = default);
}
