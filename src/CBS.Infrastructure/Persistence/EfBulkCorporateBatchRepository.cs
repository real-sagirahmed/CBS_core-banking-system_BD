using CBS.Application.Common.Interfaces;
using CBS.Domain.Corporate;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfBulkCorporateBatchRepository : IBulkCorporateBatchRepository
{
    private readonly CbsDbContext _dbContext;

    public EfBulkCorporateBatchRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<BulkCorporateBatch?> GetByIdAsync(string batchId, CancellationToken ct = default)
    {
        return await _dbContext.BulkCorporateBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);
    }

    public async Task AddAsync(BulkCorporateBatch batch, CancellationToken ct = default)
    {
        await _dbContext.BulkCorporateBatches.AddAsync(batch, ct);
    }

    public Task UpdateAsync(BulkCorporateBatch batch, CancellationToken ct = default)
    {
        _dbContext.BulkCorporateBatches.Update(batch);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<BulkCorporateBatch>> GetBatchesByCorporateAccountAsync(string accountNumber, CancellationToken ct = default)
    {
        var accNo = AccountNumber.From(accountNumber.Trim());
        return await _dbContext.BulkCorporateBatches
            .Include(b => b.Items)
            .Where(b => b.CorporateDebitAccountNumber == accNo)
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToListAsync(ct);
    }
}
