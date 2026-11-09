using CBS.Domain.PaymentRails;

namespace CBS.Application.Common.Interfaces;

public interface IBeftnRepository
{
    Task<BeftnBatchItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<BeftnBatchItem>> GetByBatchIdAsync(string batchId, CancellationToken cancellationToken = default);
    Task AddAsync(BeftnBatchItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(BeftnBatchItem item, CancellationToken cancellationToken = default);
}
