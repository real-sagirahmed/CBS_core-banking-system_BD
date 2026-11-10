using CBS.Application.Common.Interfaces;
using CBS.Domain.Treasury;
using System.Collections.Concurrent;

namespace CBS.Infrastructure.Persistence;

public class InMemoryTreasuryRepository : ITreasuryRepository
{
    private readonly ConcurrentDictionary<string, TreasuryDeal> _deals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TreasurySecurity> _securities = new(StringComparer.OrdinalIgnoreCase);

    public Task<TreasuryDeal?> GetDealByReferenceAsync(string dealReference, CancellationToken cancellationToken = default)
    {
        _deals.TryGetValue(dealReference, out var deal);
        return Task.FromResult(deal);
    }

    public Task<TreasuryDeal?> GetDealByReferenceForUpdateAsync(string dealReference, CancellationToken cancellationToken = default)
    {
        return GetDealByReferenceAsync(dealReference, cancellationToken);
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

    public Task<TreasurySecurity?> GetSecurityByIdAsync(string securityId, CancellationToken cancellationToken = default)
    {
        _securities.TryGetValue(securityId, out var sec);
        return Task.FromResult(sec);
    }

    public Task<TreasurySecurity?> GetSecurityByIdForUpdateAsync(string securityId, CancellationToken cancellationToken = default)
    {
        return GetSecurityByIdAsync(securityId, cancellationToken);
    }

    public Task AddSecurityAsync(TreasurySecurity security, CancellationToken cancellationToken = default)
    {
        if (!_securities.TryAdd(security.SecurityId, security))
        {
            throw new InvalidOperationException($"Treasury Security '{security.SecurityId}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateSecurityAsync(TreasurySecurity security, CancellationToken cancellationToken = default)
    {
        _securities[security.SecurityId] = security;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TreasurySecurity>> GetAllSecuritiesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TreasurySecurity> list = _securities.Values.ToList();
        return Task.FromResult(list);
    }
}
