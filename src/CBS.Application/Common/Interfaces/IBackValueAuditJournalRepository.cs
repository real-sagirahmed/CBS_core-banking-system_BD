using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.BackValue;

namespace CBS.Application.Common.Interfaces;

public interface IBackValueAuditJournalRepository
{
    Task AddJournalEntryAsync(BackValueAuditJournalEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<BackValueAuditJournalEntry>> GetJournalEntriesByDateAsync(DateOnly operatingDate, string? branchCode = null, CancellationToken ct = default);
}
