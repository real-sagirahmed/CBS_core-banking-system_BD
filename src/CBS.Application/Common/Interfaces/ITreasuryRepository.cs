using CBS.Domain.Treasury;

namespace CBS.Application.Common.Interfaces;

public interface ITreasuryRepository
{
    Task<TreasuryDeal?> GetDealByReferenceAsync(string dealReference, CancellationToken cancellationToken = default);
    Task AddDealAsync(TreasuryDeal deal, CancellationToken cancellationToken = default);
    Task UpdateDealAsync(TreasuryDeal deal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreasuryDeal>> GetAllDealsAsync(CancellationToken cancellationToken = default);
}
