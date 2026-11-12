using CBS.Domain.Accounts;

namespace CBS.Application.Common.Interfaces;

public interface IAccountLedgerRepository
{
    Task AddAsync(AccountLedgerEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountLedgerEntry>> GetEntriesAsync(string accountNumber, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
    Task<decimal> GetOpeningBalanceAsync(string accountNumber, DateOnly asOfDate, CancellationToken cancellationToken = default);
}
