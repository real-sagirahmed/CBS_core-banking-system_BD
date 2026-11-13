// ARCHITECTURAL INTENT: EF Core Repository implementation for TelegraphicTransfer aggregate
// enforcing BRPD Circular 12 (3.13) query patterns for single lookup, outstanding, and aging reporting.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.TelegraphicTransfers;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfTelegraphicTransferRepository : ITelegraphicTransferRepository
{
    private readonly CbsDbContext _dbContext;

    public EfTelegraphicTransferRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TelegraphicTransfer?> GetByIdAsync(string ttId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TelegraphicTransfers
            .FirstOrDefaultAsync(t => t.Id == ttId, cancellationToken);
    }

    public async Task<TelegraphicTransfer?> GetByTtNumberAsync(string ttNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TelegraphicTransfers
            .FirstOrDefaultAsync(t => t.TtNumber == ttNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<TelegraphicTransfer>> GetOutstandingTtAsync(
        string? branchCode,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TelegraphicTransfers
            .Where(t => t.Status != TtStatus.Responded && t.Status != TtStatus.Cancelled);

        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var branch = branchCode.Trim().ToUpperInvariant();
            query = query.Where(t => t.IssuingBranchCode == branch || t.DraweeBranchCode == branch);
        }

        return await query
            .OrderByDescending(t => t.IssueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TelegraphicTransfer>> GetUnrespondedTtAgingAsync(
        DateOnly asOfDate,
        int minimumDaysUnresponded,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = asOfDate.AddDays(-minimumDaysUnresponded);

        return await _dbContext.TelegraphicTransfers
            .Where(t => (t.Status == TtStatus.Issued || t.Status == TtStatus.AdviceTransmitted || t.Status == TtStatus.TelephonicallyVerified)
                        && t.IssueDate <= cutoffDate)
            .OrderBy(t => t.IssueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TelegraphicTransfer transfer, CancellationToken cancellationToken = default)
    {
        await _dbContext.TelegraphicTransfers.AddAsync(transfer, cancellationToken);
    }

    public Task UpdateAsync(TelegraphicTransfer transfer, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Entry(transfer).State == EntityState.Detached)
        {
            _dbContext.TelegraphicTransfers.Update(transfer);
        }
        return Task.CompletedTask;
    }
}
