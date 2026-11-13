// ARCHITECTURAL INTENT: EF Core Repository implementation for PaymentInstrument aggregate
// enforcing BRPD Circular 12/2024 query patterns for outstanding, unpresented, and single-instrument lookups.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.PaymentInstruments;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfPaymentInstrumentRepository : IPaymentInstrumentRepository
{
    private readonly CbsDbContext _dbContext;

    public EfPaymentInstrumentRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaymentInstrument?> GetByIdAsync(string instrumentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentInstruments
            .FirstOrDefaultAsync(p => p.Id == instrumentId, cancellationToken);
    }

    public async Task<PaymentInstrument?> GetByInstrumentNumberAsync(string instrumentNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentInstruments
            .FirstOrDefaultAsync(p => p.InstrumentNumber == instrumentNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentInstrument>> GetOutstandingInstrumentsAsync(
        string? branchCode,
        PaymentInstrumentType? type,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PaymentInstruments
            .Where(p => p.Status == PaymentInstrumentStatus.Issued);

        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var branch = branchCode.Trim().ToUpperInvariant();
            query = query.Where(p => p.IssuingBranchCode == branch || p.DraweeBranchCode == branch);
        }

        if (type.HasValue)
        {
            query = query.Where(p => p.InstrumentType == type.Value);
        }

        return await query
            .OrderByDescending(p => p.IssueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentInstrument>> GetUnpresentedInstrumentsAsync(
        DateOnly asOfDate,
        int minimumDaysUnpresented,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = asOfDate.AddDays(-minimumDaysUnpresented);

        return await _dbContext.PaymentInstruments
            .Where(p => p.Status == PaymentInstrumentStatus.Issued && p.IssueDate <= cutoffDate)
            .OrderBy(p => p.IssueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PaymentInstrument instrument, CancellationToken cancellationToken = default)
    {
        await _dbContext.PaymentInstruments.AddAsync(instrument, cancellationToken);
    }

    public Task UpdateAsync(PaymentInstrument instrument, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Entry(instrument).State == EntityState.Detached)
        {
            _dbContext.PaymentInstruments.Update(instrument);
        }
        return Task.CompletedTask;
    }
}
