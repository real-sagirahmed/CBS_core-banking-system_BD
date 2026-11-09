using CBS.Application.Common.Interfaces;
using CBS.Domain.Treasury;
using System.Collections.Concurrent;

namespace CBS.Infrastructure.Persistence;

public class InMemoryTreasuryRepository : ITreasuryRepository
{
    private readonly ConcurrentDictionary<string, TreasuryDeal> _deals = new(StringComparer.OrdinalIgnoreCase);

    public Task<TreasuryDeal?> GetDealByReferenceAsync(string dealReference, CancellationToken cancellationToken = default)
    {
        _deals.TryGetValue(dealReference, out var deal);
        return Task.FromResult(deal);
    }

    public Task AddDealAsync(TreasuryDeal deal, CancellationToken cancellationToken = default)
    {
        if (!_deals.TryAdd(deal.DealReference, deal))
        {
            throw new InvalidOperationException($"Treasury Deal '{deal.DealReference}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateDealAsync(TreasuryDeal deal, CancellationToken cancellationToken = default)
    {
        _deals[deal.DealReference] = deal;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TreasuryDeal>> GetAllDealsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TreasuryDeal> list = _deals.Values.ToList();
        return Task.FromResult(list);
    }
}
