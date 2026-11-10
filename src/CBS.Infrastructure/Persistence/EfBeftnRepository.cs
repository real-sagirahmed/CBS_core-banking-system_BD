using CBS.Application.Common.Interfaces;
using CBS.Domain.PaymentRails;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfBeftnRepository : IBeftnRepository
{
    private readonly CbsDbContext _dbContext;

    public EfBeftnRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<BeftnBatchItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BeftnBatchItems
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<BeftnBatchItem>> GetByBatchIdAsync(string batchId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BeftnBatchItems
            .Where(b => b.BatchId == batchId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BeftnBatchItem item, CancellationToken cancellationToken = default)
    {
        await _dbContext.BeftnBatchItems.AddAsync(item, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BeftnBatchItem item, CancellationToken cancellationToken = default)
    {
        _dbContext.BeftnBatchItems.Update(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
