using CBS.Application.Common.Interfaces;
using CBS.Domain.TradeFinance;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfTbmlPriceBenchmarkRepository : ITbmlPriceBenchmarkRepository
{
    private readonly CbsDbContext _dbContext;

    public EfTbmlPriceBenchmarkRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TbmlPriceBenchmark?> GetByHsCodeAsync(string hsCode, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TbmlPriceBenchmarks
            .FirstOrDefaultAsync(b => b.HsCode == hsCode.Trim(), cancellationToken);
    }

    public async Task<IReadOnlyList<TbmlPriceBenchmark>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.TbmlPriceBenchmarks
            .OrderBy(b => b.HsCode)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TbmlPriceBenchmark benchmark, CancellationToken cancellationToken = default)
    {
        await _dbContext.TbmlPriceBenchmarks.AddAsync(benchmark, cancellationToken);
    }
}
