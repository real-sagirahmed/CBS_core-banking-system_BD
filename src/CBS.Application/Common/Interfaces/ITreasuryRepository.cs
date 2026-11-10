using CBS.Domain.Treasury;

namespace CBS.Application.Common.Interfaces;

public interface ITreasuryRepository
{
    Task<TreasuryDeal?> GetDealByReferenceAsync(string dealReference, CancellationToken cancellationToken = default);
    Task<TreasuryDeal?> GetDealByReferenceForUpdateAsync(string dealReference, CancellationToken cancellationToken = default);
    Task AddDealAsync(TreasuryDeal deal, CancellationToken cancellationToken = default);
    Task UpdateDealAsync(TreasuryDeal deal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreasuryDeal>> GetAllDealsAsync(CancellationToken cancellationToken = default);

    Task<TreasurySecurity?> GetSecurityByIdAsync(string securityId, CancellationToken cancellationToken = default);
    Task<TreasurySecurity?> GetSecurityByIdForUpdateAsync(string securityId, CancellationToken cancellationToken = default);
    Task AddSecurityAsync(TreasurySecurity security, CancellationToken cancellationToken = default);
    Task UpdateSecurityAsync(TreasurySecurity security, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreasurySecurity>> GetAllSecuritiesAsync(CancellationToken cancellationToken = default);
}
