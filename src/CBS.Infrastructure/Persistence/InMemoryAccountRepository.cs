using System.Collections.Concurrent;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Accounts;
using CBS.Domain.ValueObjects;

namespace CBS.Infrastructure.Persistence;

public class InMemoryAccountRepository : IAccountRepository
{
    private readonly ConcurrentDictionary<string, DepositAccount> _accounts = new();

    public Task<DepositAccount?> GetByAccountNumberAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountNumber);
        _accounts.TryGetValue(accountNumber.Value, out var account);
        return Task.FromResult(account);
    }

    public Task<IEnumerable<DepositAccount>> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cif);
        var matches = _accounts.Values.Where(a => a.Cif.Value == cif.Value);
        return Task.FromResult(matches);
    }

    public Task AddAsync(DepositAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (!_accounts.TryAdd(account.Id.Value, account))
        {
            throw new InvalidOperationException($"Account '{account.Id.Value}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(DepositAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        _accounts[account.Id.Value] = account;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<DepositAccount>> GetAllAccountsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<DepositAccount>>(_accounts.Values.ToList());
    }
}
