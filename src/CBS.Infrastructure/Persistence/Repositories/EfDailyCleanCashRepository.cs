using System;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Reporting;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfDailyCleanCashRepository : IDailyCleanCashRepository
{
    private readonly CbsDbContext _dbContext;

    public EfDailyCleanCashRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DailyCleanCashSheet?> GetByIdAsync(string sheetId, CancellationToken ct = default)
    {
        return await _dbContext.DailyCleanCashSheets
            .FirstOrDefaultAsync(s => s.Id == sheetId.Trim().ToUpperInvariant(), ct);
    }

    public async Task<DailyCleanCashSheet?> GetByBranchAndDateAsync(string branchCode, DateOnly operatingDate, CancellationToken ct = default)
    {
        var normalized = branchCode.Trim();
        return await _dbContext.DailyCleanCashSheets
            .FirstOrDefaultAsync(s => s.BranchCode == normalized && s.OperatingDate == operatingDate, ct);
    }

    public async Task AddAsync(DailyCleanCashSheet sheet, CancellationToken ct = default)
    {
        await _dbContext.DailyCleanCashSheets.AddAsync(sheet, ct);
    }

    public Task UpdateAsync(DailyCleanCashSheet sheet, CancellationToken ct = default)
    {
        _dbContext.DailyCleanCashSheets.Update(sheet);
        return Task.CompletedTask;
    }
}
