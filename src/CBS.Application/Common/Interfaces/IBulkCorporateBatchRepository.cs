using CBS.Domain.Corporate;

namespace CBS.Application.Common.Interfaces;

public interface IBulkCorporateBatchRepository
{
    Task<BulkCorporateBatch?> GetByIdAsync(string batchId, CancellationToken ct = default);
    Task AddAsync(BulkCorporateBatch batch, CancellationToken ct = default);
    Task UpdateAsync(BulkCorporateBatch batch, CancellationToken ct = default);
    Task<IEnumerable<BulkCorporateBatch>> GetBatchesByCorporateAccountAsync(string accountNumber, CancellationToken ct = default);
}
