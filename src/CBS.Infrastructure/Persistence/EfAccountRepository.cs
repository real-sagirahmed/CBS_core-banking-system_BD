using CBS.Application.Common.Interfaces;
using CBS.Domain.Accounts;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfAccountRepository : IAccountRepository
{
    private readonly CbsDbContext _dbContext;

    public EfAccountRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<DepositAccount?> GetByAccountNumberAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DepositAccounts
            .FirstOrDefaultAsync(a => a.Id == accountNumber, cancellationToken);
    }

    public async Task<DepositAccount?> GetByAccountNumberForUpdateAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsNpgsql())
        {
            return await _dbContext.DepositAccounts
                .FromSqlInterpolated($"SELECT * FROM deposit_accounts WHERE account_number = {accountNumber.Value} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await _dbContext.DepositAccounts
            .FirstOrDefaultAsync(a => a.Id == accountNumber, cancellationToken);
    }

    public async Task<IEnumerable<DepositAccount>> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DepositAccounts
            .Where(a => a.Cif == cif)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DepositAccount account, CancellationToken cancellationToken = default)
    {
        await _dbContext.DepositAccounts.AddAsync(account, cancellationToken);
    }

    public Task UpdateAsync(DepositAccount account, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(account);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.DepositAccounts.Update(account);
        }
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<DepositAccount>> GetAllAccountsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.DepositAccounts
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
