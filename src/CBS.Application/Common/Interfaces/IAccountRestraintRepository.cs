using CBS.Domain.Accounts;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface IAccountRestraintRepository
{
    Task<AccountRestraint?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IEnumerable<AccountRestraint>> GetByAccountNumberAsync(AccountNumber accountNumber, CancellationToken ct = default);
    Task<IEnumerable<AccountRestraint>> GetActiveRestraintsByAccountNumberAsync(AccountNumber accountNumber, CancellationToken ct = default);
    Task AddAsync(AccountRestraint restraint, CancellationToken ct = default);
    Task UpdateAsync(AccountRestraint restraint, CancellationToken ct = default);
}
