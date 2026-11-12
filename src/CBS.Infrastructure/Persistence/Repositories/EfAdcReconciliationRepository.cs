using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Adc;
using CBS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfAdcReconciliationRepository : IAdcReconciliationRepository
{
    private readonly CbsDbContext _dbContext;

    public EfAdcReconciliationRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdcReconciliationBatch?> GetByIdAsync(string batchId, CancellationToken ct = default)
    {
        return await _dbContext.AdcReconciliationBatches
            .FirstOrDefaultAsync(b => b.Id == batchId.Trim().ToUpperInvariant(), ct);
    }

    public async Task<IReadOnlyList<AdcReconciliationBatch>> GetPendingBatchesAsync(CancellationToken ct = default)
    {
        return await _dbContext.AdcReconciliationBatches
            .Where(b => b.Status == ReconciliationBatchStatus.Open || b.Status == ReconciliationBatchStatus.DiscrepancyPendingRectification)
            .ToListAsync(ct);
    }

    public async Task AddAsync(AdcReconciliationBatch batch, CancellationToken ct = default)
    {
        await _dbContext.AdcReconciliationBatches.AddAsync(batch, ct);
    }

    public Task UpdateAsync(AdcReconciliationBatch batch, CancellationToken ct = default)
    {
        _dbContext.AdcReconciliationBatches.Update(batch);
        return Task.CompletedTask;
    }
}
