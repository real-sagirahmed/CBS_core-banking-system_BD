using CBS.Application.Common.Interfaces;
using CBS.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfAccountLedgerRepository : IAccountLedgerRepository
{
    private readonly CbsDbContext _dbContext;

    public EfAccountLedgerRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(AccountLedgerEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry, nameof(entry));
        await _dbContext.AccountLedgerEntries.AddAsync(entry, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountLedgerEntry>> GetEntriesAsync(
        string accountNumber,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AccountLedgerEntries
            .AsNoTracking()
            .Where(e => e.AccountNumber == accountNumber && e.ValueDate >= fromDate && e.ValueDate <= toDate)
            .OrderBy(e => e.BookingTimeUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetOpeningBalanceAsync(
        string accountNumber,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var priorEntry = await _dbContext.AccountLedgerEntries
            .AsNoTracking()
            .Where(e => e.AccountNumber == accountNumber && e.ValueDate < asOfDate)
            .OrderByDescending(e => e.BookingTimeUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return priorEntry?.BalanceAfter.Amount ?? 0m;
    }
}
