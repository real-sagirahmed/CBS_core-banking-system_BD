using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Adc;

namespace CBS.Application.Common.Interfaces;

public interface IAdcReconciliationRepository
{
    Task<AdcReconciliationBatch?> GetByIdAsync(string batchId, CancellationToken ct = default);
    Task<IReadOnlyList<AdcReconciliationBatch>> GetPendingBatchesAsync(CancellationToken ct = default);
    Task AddAsync(AdcReconciliationBatch batch, CancellationToken ct = default);
    Task UpdateAsync(AdcReconciliationBatch batch, CancellationToken ct = default);
}
