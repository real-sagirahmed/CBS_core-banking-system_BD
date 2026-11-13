// ARCHITECTURAL INTENT: EF Core Repository implementation for PaymentInstrumentStockBook aggregate
// enforcing joint-custody stock book tracking and FIFO leaf allocation under BRPD Circular 12/2024.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.PaymentInstruments;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfPaymentInstrumentStockRepository : IPaymentInstrumentStockRepository
{
    private readonly CbsDbContext _dbContext;

    public EfPaymentInstrumentStockRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaymentInstrumentStockBook?> GetByIdAsync(string bookId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentInstrumentStockBooks
            .FirstOrDefaultAsync(s => s.Id == bookId, cancellationToken);
    }

    public async Task<PaymentInstrumentStockBook?> GetActiveBookForBranchAsync(
        string branchCode,
        PaymentInstrumentType type,
        CancellationToken cancellationToken = default)
    {
        var branch = branchCode.Trim().ToUpperInvariant();
        return await _dbContext.PaymentInstrumentStockBooks
            .Where(s => s.BranchCode == branch && s.InstrumentType == type && s.IssuedLeavesCount < s.TotalLeaves)
            .OrderBy(s => s.AllocatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentInstrumentStockBook>> GetStockBooksByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        var branch = branchCode.Trim().ToUpperInvariant();
        return await _dbContext.PaymentInstrumentStockBooks
            .Where(s => s.BranchCode == branch)
            .OrderByDescending(s => s.AllocatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PaymentInstrumentStockBook stockBook, CancellationToken cancellationToken = default)
    {
        await _dbContext.PaymentInstrumentStockBooks.AddAsync(stockBook, cancellationToken);
    }

    public Task UpdateAsync(PaymentInstrumentStockBook stockBook, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Entry(stockBook).State == EntityState.Detached)
        {
            _dbContext.PaymentInstrumentStockBooks.Update(stockBook);
        }
        return Task.CompletedTask;
    }
}
