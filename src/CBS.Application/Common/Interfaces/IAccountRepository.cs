using CBS.Domain.Accounts;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface IAccountRepository
{
    Task<DepositAccount?> GetByAccountNumberAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default);
    Task<DepositAccount?> GetByAccountNumberForUpdateAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default)
        => GetByAccountNumberAsync(accountNumber, cancellationToken);
    Task<IEnumerable<DepositAccount>> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task AddAsync(DepositAccount account, CancellationToken cancellationToken = default);
    Task UpdateAsync(DepositAccount account, CancellationToken cancellationToken = default);
    Task<IEnumerable<DepositAccount>> GetAllAccountsAsync(CancellationToken cancellationToken = default);
}
