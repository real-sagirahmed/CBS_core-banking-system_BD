using CBS.Application.Common.Interfaces;
using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfLoanAccountRepository : ILoanAccountRepository
{
    private readonly CbsDbContext _dbContext;

    public EfLoanAccountRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<LoanAccount?> GetByAccountNumberAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanAccounts
            .FirstOrDefaultAsync(l => l.Id == accountNumber, cancellationToken);
    }

    public async Task<LoanAccount?> GetByAccountNumberForUpdateAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsNpgsql())
        {
            return await _dbContext.LoanAccounts
                .FromSqlInterpolated($"SELECT * FROM loan_accounts WHERE account_number = {accountNumber.Value} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await _dbContext.LoanAccounts
            .FirstOrDefaultAsync(l => l.Id == accountNumber, cancellationToken);
    }

    public async Task<IEnumerable<LoanAccount>> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanAccounts
            .Where(l => l.Cif == cif)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(LoanAccount loan, CancellationToken cancellationToken = default)
    {
        await _dbContext.LoanAccounts.AddAsync(loan, cancellationToken);
    }

    public Task UpdateAsync(LoanAccount loan, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(loan);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.LoanAccounts.Update(loan);
        }
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<LoanAccount>> GetAllLoansAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanAccounts
            .ToListAsync(cancellationToken);
    }
}
