using CBS.Domain.SecurityAudit;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface ISecurityAuditRepository
{
    Task<UserSession?> GetActiveSessionByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserSession?> GetSessionByIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task AddSessionAsync(UserSession session, CancellationToken cancellationToken = default);
    Task UpdateSessionAsync(UserSession session, CancellationToken cancellationToken = default);

    Task<long> GetLatestAuditSequenceAsync(CancellationToken cancellationToken = default);
    Task<string> GetLatestAuditHashAsync(CancellationToken cancellationToken = default);
    Task AddAuditEntryAsync(AuditJournalEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditJournalEntry>> GetAuditEntriesAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default);

    Task AddBfiuReportAsync(BfiuRegulatoryReport report, CancellationToken cancellationToken = default);
    Task<BfiuRegulatoryReport?> GetBfiuReportByReferenceAsync(string reportReference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BfiuRegulatoryReport>> GetAllBfiuReportsAsync(CancellationToken cancellationToken = default);
}
