using System.Collections.Concurrent;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;

namespace CBS.Infrastructure.Persistence;

public class InMemoryLoanAccountRepository : ILoanAccountRepository
{
    private readonly ConcurrentDictionary<string, LoanAccount> _loans = new();

    public Task<LoanAccount?> GetByAccountNumberAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountNumber);
        _loans.TryGetValue(accountNumber.Value, out var loan);
        return Task.FromResult(loan);
    }

    public Task<IEnumerable<LoanAccount>> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cif);
        var matches = _loans.Values.Where(l => l.Cif.Value == cif.Value);
        return Task.FromResult(matches);
    }

    public Task AddAsync(LoanAccount loan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loan);
        if (!_loans.TryAdd(loan.Id.Value, loan))
        {
            throw new InvalidOperationException($"Loan account '{loan.Id.Value}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(LoanAccount loan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loan);
        _loans[loan.Id.Value] = loan;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<LoanAccount>> GetAllLoansAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<LoanAccount>>(_loans.Values.ToList());
    }
}
