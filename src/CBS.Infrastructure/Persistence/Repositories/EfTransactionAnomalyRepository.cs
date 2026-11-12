using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfTransactionAnomalyRepository : ITransactionAnomalyRepository
{
    private readonly CbsDbContext _dbContext;

    public EfTransactionAnomalyRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TransactionAnomalyRecord?> GetByIdAsync(string anomalyId, CancellationToken ct = default)
    {
        return await _dbContext.TransactionAnomalies
            .FirstOrDefaultAsync(a => a.Id == anomalyId.Trim().ToUpperInvariant(), ct);
    }

    public async Task<IReadOnlyList<TransactionAnomalyRecord>> GetPendingAnomaliesAsync(CancellationToken ct = default)
    {
        return await _dbContext.TransactionAnomalies
            .Where(a => a.Status == AnomalyResolutionStatus.DetectedPendingInvestigation)
            .OrderByDescending(a => a.DetectedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(TransactionAnomalyRecord record, CancellationToken ct = default)
    {
        await _dbContext.TransactionAnomalies.AddAsync(record, ct);
    }

    public Task UpdateAsync(TransactionAnomalyRecord record, CancellationToken ct = default)
    {
        _dbContext.TransactionAnomalies.Update(record);
        return Task.CompletedTask;
    }
}
