// ARCHITECTURAL INTENT: EF Core repository implementation for ChequeDishonourRecord
// per BRPD Circular No. 12 Section 1.5.3.

using CBS.Application.Common.Interfaces;
using CBS.Domain.Clearing;
using CBS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfChequeDishonourRepository : IChequeDishonourRepository
{
    private readonly CbsDbContext _dbContext;

    public EfChequeDishonourRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ChequeDishonourRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChequeDishonourRecords
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<ChequeDishonourRecord?> GetByMemoReferenceAsync(string memoReference, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChequeDishonourRecords
            .FirstOrDefaultAsync(x => x.MemoReferenceNumber == memoReference, cancellationToken);
    }

    public async Task<IReadOnlyList<ChequeDishonourRecord>> GetByDrawerAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChequeDishonourRecords
            .Where(x => x.DrawerAccountNumber == accountNumber)
            .OrderByDescending(x => x.DishonourDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChequeDishonourRecord>> GetByDrawerCifAsync(string cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChequeDishonourRecords
            .Where(x => x.DrawerCif == cif)
            .OrderByDescending(x => x.DishonourDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChequeDishonourRecord>> GetPendingSection138NoticesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChequeDishonourRecords
            .Where(x => x.NoticeStatus == NiActNoticeStatus.NotIssued || x.NoticeStatus == NiActNoticeStatus.Dispatched)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ChequeDishonourRecord record, CancellationToken cancellationToken = default)
    {
        await _dbContext.ChequeDishonourRecords.AddAsync(record, cancellationToken);
    }

    public Task UpdateAsync(ChequeDishonourRecord record, CancellationToken cancellationToken = default)
    {
        _dbContext.ChequeDishonourRecords.Update(record);
        return Task.CompletedTask;
    }
}
