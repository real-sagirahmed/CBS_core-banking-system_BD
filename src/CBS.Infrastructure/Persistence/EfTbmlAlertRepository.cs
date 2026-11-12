using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.TradeFinance;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfTbmlAlertRepository : ITbmlAlertRepository
{
    private readonly CbsDbContext _dbContext;

    public EfTbmlAlertRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TbmlAlertRecord?> GetByIdAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TbmlAlertRecords
            .FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken);
    }

    public async Task<IReadOnlyList<TbmlAlertRecord>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.TbmlAlertRecords
            .Where(a => a.Status == TbmlAlertStatus.Active || a.Status == TbmlAlertStatus.UnderInvestigation)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TbmlAlertRecord>> GetAlertsByReferenceAsync(string referenceNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TbmlAlertRecords
            .Where(a => a.ReferenceNumber == referenceNumber.Trim().ToUpperInvariant())
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TbmlAlertRecord alert, CancellationToken cancellationToken = default)
    {
        await _dbContext.TbmlAlertRecords.AddAsync(alert, cancellationToken);
    }

    public Task UpdateAsync(TbmlAlertRecord alert, CancellationToken cancellationToken = default)
    {
        _dbContext.TbmlAlertRecords.Update(alert);
        return Task.CompletedTask;
    }
}
