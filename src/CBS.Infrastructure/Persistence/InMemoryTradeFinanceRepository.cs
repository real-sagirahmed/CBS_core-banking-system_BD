using CBS.Application.Common.Interfaces;
using CBS.Domain.TradeFinance;
using System.Collections.Concurrent;

namespace CBS.Infrastructure.Persistence;

public class InMemoryTradeFinanceRepository : ITradeFinanceRepository
{
    private readonly ConcurrentDictionary<string, DocumentaryCredit> _lcs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ShippingGuarantee> _sgs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ExpRegulatoryTracking> _exps = new(StringComparer.OrdinalIgnoreCase);

    public Task<DocumentaryCredit?> GetLcByNumberAsync(string lcNumber, CancellationToken cancellationToken = default)
    {
        _lcs.TryGetValue(lcNumber, out var lc);
        return Task.FromResult(lc);
    }

    public Task AddLcAsync(DocumentaryCredit lc, CancellationToken cancellationToken = default)
    {
        if (!_lcs.TryAdd(lc.LcNumber, lc))
        {
            throw new InvalidOperationException($"Documentary Credit '{lc.LcNumber}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateLcAsync(DocumentaryCredit lc, CancellationToken cancellationToken = default)
    {
        _lcs[lc.LcNumber] = lc;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentaryCredit>> GetAllLcsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DocumentaryCredit> list = _lcs.Values.ToList();
        return Task.FromResult(list);
    }

    public Task<ShippingGuarantee?> GetSgByReferenceAsync(string sgReference, CancellationToken cancellationToken = default)
    {
        _sgs.TryGetValue(sgReference, out var sg);
        return Task.FromResult(sg);
    }

    public Task AddSgAsync(ShippingGuarantee sg, CancellationToken cancellationToken = default)
    {
        if (!_sgs.TryAdd(sg.SgReference, sg))
        {
            throw new InvalidOperationException($"Shipping Guarantee '{sg.SgReference}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateSgAsync(ShippingGuarantee sg, CancellationToken cancellationToken = default)
    {
        _sgs[sg.SgReference] = sg;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ShippingGuarantee>> GetAllSgsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ShippingGuarantee> list = _sgs.Values.ToList();
        return Task.FromResult(list);
    }

    public Task<ExpRegulatoryTracking?> GetExpByNumberAsync(string expNumber, CancellationToken cancellationToken = default)
    {
        _exps.TryGetValue(expNumber, out var exp);
        return Task.FromResult(exp);
    }

    public Task AddExpAsync(ExpRegulatoryTracking exp, CancellationToken cancellationToken = default)
    {
        if (!_exps.TryAdd(exp.ExpNumber, exp))
        {
            throw new InvalidOperationException($"EXP Form '{exp.ExpNumber}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateExpAsync(ExpRegulatoryTracking exp, CancellationToken cancellationToken = default)
    {
        _exps[exp.ExpNumber] = exp;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ExpRegulatoryTracking>> GetAllExpsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ExpRegulatoryTracking> list = _exps.Values.ToList();
        return Task.FromResult(list);
    }
}
