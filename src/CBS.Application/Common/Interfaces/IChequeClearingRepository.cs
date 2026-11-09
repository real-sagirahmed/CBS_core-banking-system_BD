using CBS.Domain.PaymentRails;

namespace CBS.Application.Common.Interfaces;

public interface IChequeClearingRepository
{
    Task<ChequeClearingItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ChequeClearingItem>> GetByBatchIdAsync(string batchId, CancellationToken cancellationToken = default);
    Task AddAsync(ChequeClearingItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(ChequeClearingItem item, CancellationToken cancellationToken = default);
}
