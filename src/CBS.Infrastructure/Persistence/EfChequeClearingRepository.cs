using CBS.Application.Common.Interfaces;
using CBS.Domain.PaymentRails;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfChequeClearingRepository : IChequeClearingRepository
{
    private readonly CbsDbContext _dbContext;

    public EfChequeClearingRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ChequeClearingItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChequeClearingItems
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<ChequeClearingItem>> GetByBatchIdAsync(string batchId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChequeClearingItems
            .Where(c => c.BatchId == batchId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ChequeClearingItem item, CancellationToken cancellationToken = default)
    {
        await _dbContext.ChequeClearingItems.AddAsync(item, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ChequeClearingItem item, CancellationToken cancellationToken = default)
    {
        _dbContext.ChequeClearingItems.Update(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
