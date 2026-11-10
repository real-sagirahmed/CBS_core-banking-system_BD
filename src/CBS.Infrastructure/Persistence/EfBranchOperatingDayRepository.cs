using System.Collections.Concurrent;
using CBS.Application.Common.Interfaces;
using CBS.Domain.GeneralLedger;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfBranchOperatingDayRepository : IBranchOperatingDayRepository
{
    private readonly CbsDbContext _dbContext;
    private static readonly ConcurrentDictionary<string, int> PendingVouchers = new();

    public EfBranchOperatingDayRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<BranchOperatingDay?> GetByBranchCodeAsync(string branchCode, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BranchOperatingDays
            .FirstOrDefaultAsync(b => b.Id == branchCode, cancellationToken);
    }

    public async Task AddAsync(BranchOperatingDay operatingDay, CancellationToken cancellationToken = default)
    {
        await _dbContext.BranchOperatingDays.AddAsync(operatingDay, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BranchOperatingDay operatingDay, CancellationToken cancellationToken = default)
    {
        _dbContext.BranchOperatingDays.Update(operatingDay);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<int> GetPendingVouchersCountAsync(string branchCode, CancellationToken cancellationToken = default)
    {
        PendingVouchers.TryGetValue(branchCode, out int count);
        return Task.FromResult(count);
    }

    public Task SetPendingVouchersCountAsync(string branchCode, int count, CancellationToken cancellationToken = default)
    {
        PendingVouchers[branchCode] = count;
        return Task.CompletedTask;
    }
}
