using CBS.Application.Common.Interfaces;
using CBS.Domain.Reconciliation;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfReconciliationRepository : IReconciliationRepository
{
    private readonly CbsDbContext _dbContext;

    public EfReconciliationRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<SettlementReconciliationBatch?> GetBatchByIdAsync(string batchId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SettlementReconciliationBatches
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
    }

    public async Task<IReadOnlyList<SettlementReconciliationBatch>> GetAllBatchesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SettlementReconciliationBatches
            .OrderByDescending(b => b.ExecutedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddBatchAsync(SettlementReconciliationBatch batch, CancellationToken cancellationToken = default)
    {
        await _dbContext.SettlementReconciliationBatches.AddAsync(batch, cancellationToken);
    }

    public async Task<DisputeClaim?> GetDisputeByIdAsync(string claimId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DisputeClaims
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);
    }

    public async Task<IReadOnlyList<DisputeClaim>> GetAllDisputesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.DisputeClaims
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddDisputeAsync(DisputeClaim claim, CancellationToken cancellationToken = default)
    {
        await _dbContext.DisputeClaims.AddAsync(claim, cancellationToken);
    }
}
