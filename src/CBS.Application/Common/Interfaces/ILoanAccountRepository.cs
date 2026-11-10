using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface ILoanAccountRepository
{
    Task<LoanAccount?> GetByAccountNumberAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default);
    Task<LoanAccount?> GetByAccountNumberForUpdateAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default)
        => GetByAccountNumberAsync(accountNumber, cancellationToken);
    Task<IEnumerable<LoanAccount>> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task AddAsync(LoanAccount loan, CancellationToken cancellationToken = default);
    Task UpdateAsync(LoanAccount loan, CancellationToken cancellationToken = default);
    Task<IEnumerable<LoanAccount>> GetAllLoansAsync(CancellationToken cancellationToken = default);
}
