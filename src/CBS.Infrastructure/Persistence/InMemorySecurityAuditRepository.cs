using CBS.Application.Common.Interfaces;
using CBS.Domain.SecurityAudit;
using CBS.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace CBS.Infrastructure.Persistence;

public class InMemorySecurityAuditRepository : ISecurityAuditRepository
{
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _activeUserSessions = new(StringComparer.OrdinalIgnoreCase); // UserId -> SessionId (Atomic BRPD 10.3.5-viii Lock)
    private readonly List<AuditJournalEntry> _auditLog = new();
    private readonly object _auditLock = new();
    private readonly ConcurrentDictionary<string, BfiuRegulatoryReport> _bfiuReports = new(StringComparer.OrdinalIgnoreCase);

    public Task<UserSession?> GetActiveSessionByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (_activeUserSessions.TryGetValue(userId, out var activeSessionId) &&
            _sessions.TryGetValue(activeSessionId, out var session) &&
            session.IsActive)
        {
            return Task.FromResult<UserSession?>(session);
        }

        var activeSession = _sessions.Values.FirstOrDefault(s =>
            string.Equals(s.UserId, userId, StringComparison.OrdinalIgnoreCase) && s.IsActive);
        return Task.FromResult(activeSession);
    }

    public Task<UserSession?> GetSessionByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task AddSessionAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        if (session.IsActive)
        {
            // Atomic check-and-bind: ensure no other session is active for this UserId
            // Aligns with PostgreSQL partial unique index:
            // CREATE UNIQUE INDEX ux_active_user_session ON user_sessions(user_id) WHERE is_active = true;
            if (!_activeUserSessions.TryAdd(session.UserId, session.SessionId))
            {
                throw new InvalidOperationException($"Security Invariant Violation [BRPD 10.3.5-viii]: Active session already exists for user '{session.UserId}'. Multiple/concurrent login is blocked.");
            }
        }

        if (!_sessions.TryAdd(session.SessionId, session))
        {
            if (session.IsActive)
            {
                _activeUserSessions.TryRemove(session.UserId, out _);
            }
            throw new InvalidOperationException($"Session '{session.SessionId}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateSessionAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        _sessions[session.SessionId] = session;
        if (!session.IsActive)
        {
            _activeUserSessions.TryRemove(new KeyValuePair<string, string>(session.UserId, session.SessionId));
        }
        return Task.CompletedTask;
    }

    public Task<long> GetLatestAuditSequenceAsync(CancellationToken cancellationToken = default)
    {
        lock (_auditLock)
        {
            if (_auditLog.Count == 0) return Task.FromResult(0L);
            return Task.FromResult(_auditLog[^1].SequenceNumber);
        }
    }

    public Task<string> GetLatestAuditHashAsync(CancellationToken cancellationToken = default)
    {
        lock (_auditLock)
        {
            if (_auditLog.Count == 0) return Task.FromResult(AuditJournalEntry.GenesisHash);
            return Task.FromResult(_auditLog[^1].CurrentEntryHash);
        }
    }

    public Task AddAuditEntryAsync(AuditJournalEntry entry, CancellationToken cancellationToken = default)
    {
        lock (_auditLock)
        {
            _auditLog.Add(entry);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditJournalEntry>> GetAuditEntriesAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default)
    {
        lock (_auditLock)
        {
            IEnumerable<AuditJournalEntry> query = _auditLog;
            if (fromUtc.HasValue)
            {
                query = query.Where(e => e.TimestampUtc >= fromUtc.Value);
            }
            if (toUtc.HasValue)
            {
                query = query.Where(e => e.TimestampUtc <= toUtc.Value);
            }
            IReadOnlyList<AuditJournalEntry> list = query.OrderBy(e => e.SequenceNumber).ToList();
            return Task.FromResult(list);
        }
    }

    public Task AddBfiuReportAsync(BfiuRegulatoryReport report, CancellationToken cancellationToken = default)
    {
        if (!_bfiuReports.TryAdd(report.ReportReference, report))
        {
            throw new InvalidOperationException($"BFIU Report '{report.ReportReference}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task<BfiuRegulatoryReport?> GetBfiuReportByReferenceAsync(string reportReference, CancellationToken cancellationToken = default)
    {
        _bfiuReports.TryGetValue(reportReference, out var report);
        return Task.FromResult(report);
    }

    public Task<IReadOnlyList<BfiuRegulatoryReport>> GetAllBfiuReportsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BfiuRegulatoryReport> list = _bfiuReports.Values.ToList();
        return Task.FromResult(list);
    }
}
