using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.BackValue;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfBackValueAuditJournalRepository : IBackValueAuditJournalRepository
{
    private readonly CbsDbContext _dbContext;

    public EfBackValueAuditJournalRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddJournalEntryAsync(BackValueAuditJournalEntry entry, CancellationToken ct = default)
    {
        await _dbContext.BackValueAuditJournalEntries.AddAsync(entry, ct);
    }

    public async Task<IReadOnlyList<BackValueAuditJournalEntry>> GetJournalEntriesByDateAsync(DateOnly operatingDate, string? branchCode = null, CancellationToken ct = default)
    {
        var query = _dbContext.BackValueAuditJournalEntries
            .Where(j => j.BookingDate == operatingDate);

        if (!string.IsNullOrWhiteSpace(branchCode) && !string.Equals(branchCode, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedBranch = branchCode.Trim().ToUpperInvariant();
            query = query.Where(j => j.BranchCode == normalizedBranch);
        }

        return await query.OrderByDescending(j => j.ApprovalTimeUtc).ToListAsync(ct);
    }
}
