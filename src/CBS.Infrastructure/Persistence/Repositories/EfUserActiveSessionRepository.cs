using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfUserActiveSessionRepository : IUserActiveSessionRepository
{
    private readonly CbsDbContext _dbContext;

    public EfUserActiveSessionRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserActiveSession?> GetActiveSessionByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var normalized = userId.Trim().ToUpperInvariant();
        return await _dbContext.UserActiveSessions
            .Where(s => s.UserId == normalized && s.IsActive)
            .OrderByDescending(s => s.LoginTimeUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<UserActiveSession?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default)
    {
        return await _dbContext.UserActiveSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
    }

    public async Task<IReadOnlyList<UserActiveSession>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        return await _dbContext.UserActiveSessions
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.LoginTimeUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(UserActiveSession session, CancellationToken ct = default)
    {
        await _dbContext.UserActiveSessions.AddAsync(session, ct);
    }

    public Task UpdateAsync(UserActiveSession session, CancellationToken ct = default)
    {
        _dbContext.UserActiveSessions.Update(session);
        return Task.CompletedTask;
    }

    public async Task TerminateActiveSessionsForUserAsync(string userId, SessionTerminationReason reason, string? notes = null, CancellationToken ct = default)
    {
        var normalized = userId.Trim().ToUpperInvariant();
        var activeSessions = await _dbContext.UserActiveSessions
            .Where(s => s.UserId == normalized && s.IsActive)
            .ToListAsync(ct);

        foreach (var s in activeSessions)
        {
            s.TerminateSession(reason, notes);
        }
    }
}
