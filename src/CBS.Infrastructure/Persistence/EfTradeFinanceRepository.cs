using CBS.Application.Common.Interfaces;
using CBS.Domain.TradeFinance;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfTradeFinanceRepository : ITradeFinanceRepository
{
    private readonly CbsDbContext _dbContext;

    public EfTradeFinanceRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<DocumentaryCredit?> GetLcByNumberAsync(string lcNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DocumentaryCredits
            .FirstOrDefaultAsync(c => c.Id == lcNumber, cancellationToken);
    }

    public async Task<DocumentaryCredit?> GetLcByNumberForUpdateAsync(string lcNumber, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsNpgsql())
        {
            return await _dbContext.DocumentaryCredits
                .FromSqlInterpolated($"SELECT * FROM trade_documentary_credits WHERE lc_number = {lcNumber} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await _dbContext.DocumentaryCredits
            .FirstOrDefaultAsync(c => c.Id == lcNumber, cancellationToken);
    }

    public async Task AddLcAsync(DocumentaryCredit lc, CancellationToken cancellationToken = default)
    {
        await _dbContext.DocumentaryCredits.AddAsync(lc, cancellationToken);
    }

    public Task UpdateLcAsync(DocumentaryCredit lc, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(lc);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.DocumentaryCredits.Update(lc);
        }
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<DocumentaryCredit>> GetAllLcsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.DocumentaryCredits
            .ToListAsync(cancellationToken);
    }

    public async Task<ShippingGuarantee?> GetSgByReferenceAsync(string sgReference, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ShippingGuarantees
            .FirstOrDefaultAsync(g => g.Id == sgReference, cancellationToken);
    }

    public async Task<ShippingGuarantee?> GetSgByReferenceForUpdateAsync(string sgReference, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsNpgsql())
        {
            return await _dbContext.ShippingGuarantees
                .FromSqlInterpolated($"SELECT * FROM trade_shipping_guarantees WHERE sg_reference = {sgReference} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await _dbContext.ShippingGuarantees
            .FirstOrDefaultAsync(g => g.Id == sgReference, cancellationToken);
    }

    public async Task AddSgAsync(ShippingGuarantee sg, CancellationToken cancellationToken = default)
    {
        await _dbContext.ShippingGuarantees.AddAsync(sg, cancellationToken);
    }

    public Task UpdateSgAsync(ShippingGuarantee sg, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(sg);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.ShippingGuarantees.Update(sg);
        }
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ShippingGuarantee>> GetAllSgsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ShippingGuarantees
            .ToListAsync(cancellationToken);
    }

    public async Task<ExpRegulatoryTracking?> GetExpByNumberAsync(string expNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ExpRegulatoryTrackings
            .FirstOrDefaultAsync(e => e.Id == expNumber, cancellationToken);
    }

    public async Task<ExpRegulatoryTracking?> GetExpByNumberForUpdateAsync(string expNumber, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsNpgsql())
        {
            return await _dbContext.ExpRegulatoryTrackings
                .FromSqlInterpolated($"SELECT * FROM trade_exp_regulatory_trackings WHERE exp_number = {expNumber} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await _dbContext.ExpRegulatoryTrackings
            .FirstOrDefaultAsync(e => e.Id == expNumber, cancellationToken);
    }

    public async Task AddExpAsync(ExpRegulatoryTracking exp, CancellationToken cancellationToken = default)
    {
        await _dbContext.ExpRegulatoryTrackings.AddAsync(exp, cancellationToken);
    }

    public Task UpdateExpAsync(ExpRegulatoryTracking exp, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(exp);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.ExpRegulatoryTrackings.Update(exp);
        }
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ExpRegulatoryTracking>> GetAllExpsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ExpRegulatoryTrackings
            .ToListAsync(cancellationToken);
    }
}
