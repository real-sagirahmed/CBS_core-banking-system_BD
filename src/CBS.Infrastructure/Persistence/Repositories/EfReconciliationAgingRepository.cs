using CBS.Application.Reconciliation;
using CBS.Domain.Reconciliation;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfReconciliationAgingRepository : IReconciliationAgingRepository
{
    private readonly CbsDbContext _dbContext;

    public EfReconciliationAgingRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ReconciliationAgingItem?> GetByTraceNumberAsync(string traceNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReconciliationAgingItems
            .FirstOrDefaultAsync(r => r.TraceOrReferenceNumber == traceNumber.Trim(), cancellationToken);
    }

    public async Task<ReconciliationAgingItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReconciliationAgingItems
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ReconciliationAgingItem>> GetUnresolvedItemsAsync(string? channelType = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ReconciliationAgingItems
            .Where(r => !r.IsResolved);

        if (!string.IsNullOrWhiteSpace(channelType))
        {
            query = query.Where(r => r.ChannelType == channelType.Trim());
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ReconciliationAgingItem item, CancellationToken cancellationToken = default)
    {
        await _dbContext.ReconciliationAgingItems.AddAsync(item, cancellationToken);
    }

    public Task UpdateAsync(ReconciliationAgingItem item, CancellationToken cancellationToken = default)
    {
        _dbContext.ReconciliationAgingItems.Update(item);
        return Task.CompletedTask;
    }
}
