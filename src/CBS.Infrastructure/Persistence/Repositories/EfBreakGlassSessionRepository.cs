using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfBreakGlassSessionRepository : IBreakGlassSessionRepository
{
    private readonly CbsDbContext _dbContext;

    public EfBreakGlassSessionRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BreakGlassEmergencySession?> GetByIdAsync(string sessionId, CancellationToken ct = default)
    {
        return await _dbContext.BreakGlassSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId.Trim().ToUpperInvariant(), ct);
    }

    public async Task<IReadOnlyList<BreakGlassEmergencySession>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        return await _dbContext.BreakGlassSessions
            .Where(s => s.Status == BreakGlassStatus.ApprovedActive)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(BreakGlassEmergencySession session, CancellationToken ct = default)
    {
        await _dbContext.BreakGlassSessions.AddAsync(session, ct);
    }

    public Task UpdateAsync(BreakGlassEmergencySession session, CancellationToken ct = default)
    {
        _dbContext.BreakGlassSessions.Update(session);
        return Task.CompletedTask;
    }
}
