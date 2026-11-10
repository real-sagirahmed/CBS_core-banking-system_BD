using CBS.Application.Common.Interfaces;
using CBS.Domain.Treasury;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfTreasuryRepository : ITreasuryRepository
{
    private readonly CbsDbContext _dbContext;

    public EfTreasuryRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<TreasuryDeal?> GetDealByReferenceAsync(string dealReference, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TreasuryDeals
            .FirstOrDefaultAsync(d => d.Id == dealReference, cancellationToken);
    }

    public async Task<TreasuryDeal?> GetDealByReferenceForUpdateAsync(string dealReference, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsNpgsql())
        {
            return await _dbContext.TreasuryDeals
                .FromSqlInterpolated($"SELECT * FROM treasury_deals WHERE deal_reference = {dealReference} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await _dbContext.TreasuryDeals
            .FirstOrDefaultAsync(d => d.Id == dealReference, cancellationToken);
    }

    public async Task AddDealAsync(TreasuryDeal deal, CancellationToken cancellationToken = default)
    {
        await _dbContext.TreasuryDeals.AddAsync(deal, cancellationToken);
    }

    public Task UpdateDealAsync(TreasuryDeal deal, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(deal);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.TreasuryDeals.Update(deal);
        }
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TreasuryDeal>> GetAllDealsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.TreasuryDeals.ToListAsync(cancellationToken);
    }

    public async Task<TreasurySecurity?> GetSecurityByIdAsync(string securityId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TreasurySecurities
            .FirstOrDefaultAsync(s => s.Id == securityId, cancellationToken);
    }

    public async Task<TreasurySecurity?> GetSecurityByIdForUpdateAsync(string securityId, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsNpgsql())
        {
            return await _dbContext.TreasurySecurities
                .FromSqlInterpolated($"SELECT * FROM treasury_securities WHERE security_id = {securityId} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await _dbContext.TreasurySecurities
            .FirstOrDefaultAsync(s => s.Id == securityId, cancellationToken);
    }

    public async Task AddSecurityAsync(TreasurySecurity security, CancellationToken cancellationToken = default)
    {
        await _dbContext.TreasurySecurities.AddAsync(security, cancellationToken);
    }

    public Task UpdateSecurityAsync(TreasurySecurity security, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(security);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.TreasurySecurities.Update(security);
        }
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TreasurySecurity>> GetAllSecuritiesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.TreasurySecurities.ToListAsync(cancellationToken);
    }
}
