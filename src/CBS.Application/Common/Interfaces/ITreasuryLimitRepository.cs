// ARCHITECTURAL INTENT: Repository contract for Treasury Limits (NOP, Dealer, Counterparty).
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 6.2 & 6.3.

using CBS.Domain.Enums;
using CBS.Domain.Treasury;

namespace CBS.Application.Common.Interfaces;

public interface ITreasuryLimitRepository
{
    Task<TreasuryLimitProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TreasuryLimitProfile?> GetActiveByTypeAndIdentifierAsync(TreasuryLimitType limitType, string identifier, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreasuryLimitProfile>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(TreasuryLimitProfile profile, CancellationToken cancellationToken = default);
    Task UpdateAsync(TreasuryLimitProfile profile, CancellationToken cancellationToken = default);
}
