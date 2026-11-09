using CBS.Application.Common.Interfaces;
using CBS.Domain.SecurityAudit;
using CBS.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace CBS.Infrastructure.Persistence;

public class InMemorySecurityAuditRepository : ISecurityAuditRepository
{
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AuditJournalEntry> _auditLog = new();
    private readonly object _auditLock = new();
    private readonly ConcurrentDictionary<string, BfiuRegulatoryReport> _bfiuReports = new(StringComparer.OrdinalIgnoreCase);

    public Task<UserSession?> GetActiveSessionByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
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
        if (!_sessions.TryAdd(session.SessionId, session))
        {
            throw new InvalidOperationException($"Session '{session.SessionId}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateSessionAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        _sessions[session.SessionId] = session;
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
