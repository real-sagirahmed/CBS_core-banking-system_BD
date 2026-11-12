using CBS.Application.Common.Interfaces;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfTradeTransactionProfileRepository : ITradeTransactionProfileRepository
{
    private readonly CbsDbContext _dbContext;

    public EfTradeTransactionProfileRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TradeTransactionProfile?> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TradeTransactionProfiles
            .FirstOrDefaultAsync(t => t.CustomerCif == cif, cancellationToken);
    }

    public async Task AddAsync(TradeTransactionProfile ttp, CancellationToken cancellationToken = default)
    {
        await _dbContext.TradeTransactionProfiles.AddAsync(ttp, cancellationToken);
    }

    public Task UpdateAsync(TradeTransactionProfile ttp, CancellationToken cancellationToken = default)
    {
        _dbContext.TradeTransactionProfiles.Update(ttp);
        return Task.CompletedTask;
    }
}
