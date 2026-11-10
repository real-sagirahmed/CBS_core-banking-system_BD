using CBS.Application.Common.Interfaces;
using CBS.Domain.SecurityAudit;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace CBS.Infrastructure.Persistence;

public class EfSecurityAuditRepository : ISecurityAuditRepository
{
    private readonly CbsDbContext _dbContext;
    // Process-level memory guard for ultra-fast concurrent race conditions before DB index constraint
    private static readonly ConcurrentDictionary<string, string> ActiveUserSessions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, UserSession> SessionCache = new(StringComparer.OrdinalIgnoreCase);

    public EfSecurityAuditRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<UserSession?> GetActiveSessionByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (ActiveUserSessions.TryGetValue(userId, out var sessionId) && SessionCache.TryGetValue(sessionId, out var cachedSession))
        {
            if (cachedSession.IsActive) return cachedSession;
        }

        return await _dbContext.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive, cancellationToken);
    }

    public async Task<UserSession?> GetSessionByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (SessionCache.TryGetValue(sessionId, out var cachedSession))
        {
            return cachedSession;
        }

        return await _dbContext.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    }

    public async Task AddSessionAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        if (session.IsActive)
        {
            // 1. Process-level race lock
            if (!ActiveUserSessions.TryAdd(session.UserId, session.SessionId))
            {
                throw new InvalidOperationException($"Security Invariant Violation [BRPD 10.3.5-viii]: Active session already exists for user '{session.UserId}'. Multiple/concurrent login is blocked.");
            }

            // 2. Database-level check
            bool hasActive = await _dbContext.UserSessions
                .AnyAsync(s => s.UserId == session.UserId && s.IsActive, cancellationToken);

            if (hasActive)
            {
                ActiveUserSessions.TryRemove(session.UserId, out _);
                throw new InvalidOperationException($"Security Invariant Violation [BRPD 10.3.5-viii]: Active session already exists for user '{session.UserId}'. Multiple/concurrent login is blocked.");
            }
        }

        try
        {
            await _dbContext.UserSessions.AddAsync(session, cancellationToken);
            SessionCache[session.SessionId] = session;
        }
        catch
        {
            if (session.IsActive)
            {
                ActiveUserSessions.TryRemove(session.UserId, out _);
            }
            SessionCache.TryRemove(session.SessionId, out _);
            throw;
        }
    }

    public Task UpdateSessionAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(session);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.UserSessions.Update(session);
        }

        SessionCache[session.SessionId] = session;

        if (!session.IsActive)
        {
            ActiveUserSessions.TryRemove(new KeyValuePair<string, string>(session.UserId, session.SessionId));
            SessionCache.TryRemove(session.SessionId, out _);
        }

        return Task.CompletedTask;
    }

    public async Task<long> GetLatestAuditSequenceAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.AuditJournalEntries
            .OrderByDescending(e => e.SequenceNumber)
            .Select(e => e.SequenceNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> GetLatestAuditHashAsync(CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.AuditJournalEntries
            .OrderByDescending(e => e.SequenceNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return entry?.CurrentEntryHash ?? AuditJournalEntry.GenesisHash;
    }

    public async Task AddAuditEntryAsync(AuditJournalEntry entry, CancellationToken cancellationToken = default)
    {
        await _dbContext.AuditJournalEntries.AddAsync(entry, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditJournalEntry>> GetAuditEntriesAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default)
    {
        IQueryable<AuditJournalEntry> query = _dbContext.AuditJournalEntries;

        if (fromUtc.HasValue)
        {
            query = query.Where(e => e.TimestampUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(e => e.TimestampUtc <= toUtc.Value);
        }

        return await query.OrderBy(e => e.SequenceNumber).ToListAsync(cancellationToken);
    }

    public async Task AddBfiuReportAsync(BfiuRegulatoryReport report, CancellationToken cancellationToken = default)
    {
        await _dbContext.BfiuRegulatoryReports.AddAsync(report, cancellationToken);
    }

    public async Task<BfiuRegulatoryReport?> GetBfiuReportByReferenceAsync(string reportReference, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BfiuRegulatoryReports
            .FirstOrDefaultAsync(r => r.Id == reportReference, cancellationToken);
    }

    public async Task<IReadOnlyList<BfiuRegulatoryReport>> GetAllBfiuReportsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.BfiuRegulatoryReports
            .OrderByDescending(r => r.ReportDate)
            .ToListAsync(cancellationToken);
    }
}
