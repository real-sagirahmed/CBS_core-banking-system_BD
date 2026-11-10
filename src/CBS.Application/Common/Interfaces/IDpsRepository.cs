using CBS.Domain.Deposits;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface IDpsRepository
{
    Task<DpsAccount?> GetByIdAsync(string dpsAccountId, CancellationToken ct = default);
    Task<DpsAccount?> GetByDpsAccountNumberAsync(string dpsAccountNumber, CancellationToken ct = default);
    Task<IEnumerable<DpsAccount>> GetByLinkedCasaAsync(string linkedCasaAccountNumber, CancellationToken ct = default);
    Task<IEnumerable<DpsAccount>> GetActiveAccountsDueForSweepAsync(DateOnly date, CancellationToken ct = default);
    Task AddAsync(DpsAccount account, CancellationToken ct = default);
    Task UpdateAsync(DpsAccount account, CancellationToken ct = default);
}
