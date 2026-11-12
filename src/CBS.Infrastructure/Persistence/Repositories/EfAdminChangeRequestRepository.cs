using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfAdminChangeRequestRepository : IAdminChangeRequestRepository
{
    private readonly CbsDbContext _dbContext;

    public EfAdminChangeRequestRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminChangeRequest?> GetByIdAsync(string requestId, CancellationToken ct = default)
    {
        return await _dbContext.AdminChangeRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);
    }

    public async Task<IReadOnlyList<AdminChangeRequest>> GetPendingRequestsAsync(CancellationToken ct = default)
    {
        return await _dbContext.AdminChangeRequests
            .Where(r => r.Status == AdminChangeStatus.PendingCheckerApproval)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(AdminChangeRequest request, CancellationToken ct = default)
    {
        await _dbContext.AdminChangeRequests.AddAsync(request, ct);
    }

    public Task UpdateAsync(AdminChangeRequest request, CancellationToken ct = default)
    {
        _dbContext.AdminChangeRequests.Update(request);
        return Task.CompletedTask;
    }
}
