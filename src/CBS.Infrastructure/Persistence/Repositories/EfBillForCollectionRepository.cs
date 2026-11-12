// ARCHITECTURAL INTENT: EF Core repository implementation for BillForCollection (OBC/IBC)
// per BRPD Circular No. 12 Sections 2.2.4 & 2.2.5.

using CBS.Application.Common.Interfaces;
using CBS.Domain.Clearing;
using CBS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfBillForCollectionRepository : IBillForCollectionRepository
{
    private readonly CbsDbContext _dbContext;

    public EfBillForCollectionRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BillForCollection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BillsForCollection
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<BillForCollection?> GetByBillNumberAsync(string billNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BillsForCollection
            .FirstOrDefaultAsync(x => x.BillNumber == billNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<BillForCollection>> GetByCustomerAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BillsForCollection
            .Where(x => x.CustomerAccountNumber == accountNumber)
            .OrderByDescending(x => x.LodgedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BillForCollection>> GetByStatusAsync(BillCollectionStatus status, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BillsForCollection
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.LodgedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BillForCollection bill, CancellationToken cancellationToken = default)
    {
        await _dbContext.BillsForCollection.AddAsync(bill, cancellationToken);
    }

    public Task UpdateAsync(BillForCollection bill, CancellationToken cancellationToken = default)
    {
        _dbContext.BillsForCollection.Update(bill);
        return Task.CompletedTask;
    }
}
