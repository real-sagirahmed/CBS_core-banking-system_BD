using System;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Reporting;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfDailyEodBalancingRepository : IDailyEodBalancingRepository
{
    private readonly CbsDbContext _dbContext;

    public EfDailyEodBalancingRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DailyEodBalancingSheet?> GetByIdAsync(string sheetId, CancellationToken ct = default)
    {
        return await _dbContext.DailyEodBalancingSheets
            .FirstOrDefaultAsync(s => s.Id == sheetId.Trim().ToUpperInvariant(), ct);
    }

    public async Task<DailyEodBalancingSheet?> GetByBranchAndDateAsync(string branchCode, DateOnly operatingDate, CancellationToken ct = default)
    {
        var normalizedBranch = branchCode.Trim();
        return await _dbContext.DailyEodBalancingSheets
            .FirstOrDefaultAsync(s => s.BranchCode == normalizedBranch && s.OperatingDate == operatingDate, ct);
    }

    public async Task AddAsync(DailyEodBalancingSheet sheet, CancellationToken ct = default)
    {
        await _dbContext.DailyEodBalancingSheets.AddAsync(sheet, ct);
    }

    public Task UpdateAsync(DailyEodBalancingSheet sheet, CancellationToken ct = default)
    {
        _dbContext.DailyEodBalancingSheets.Update(sheet);
        return Task.CompletedTask;
    }
}
