using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.BackValue;
using CBS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfBackValueTransactionRepository : IBackValueTransactionRepository
{
    private readonly CbsDbContext _dbContext;

    public EfBackValueTransactionRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BackValueTransactionRequest?> GetByIdAsync(string requestId, CancellationToken ct = default)
    {
        return await _dbContext.BackValueTransactionRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);
    }

    public async Task<IReadOnlyList<BackValueTransactionRequest>> GetPendingRequestsAsync(CancellationToken ct = default)
    {
        return await _dbContext.BackValueTransactionRequests
            .Where(r => r.Status == BackValueStatus.PendingApproval)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(BackValueTransactionRequest request, CancellationToken ct = default)
    {
        await _dbContext.BackValueTransactionRequests.AddAsync(request, ct);
    }

    public Task UpdateAsync(BackValueTransactionRequest request, CancellationToken ct = default)
    {
        _dbContext.BackValueTransactionRequests.Update(request);
        return Task.CompletedTask;
    }
}
