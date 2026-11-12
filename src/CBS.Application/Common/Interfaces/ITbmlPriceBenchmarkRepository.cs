using CBS.Domain.TradeFinance;

namespace CBS.Application.Common.Interfaces;

public interface ITbmlPriceBenchmarkRepository
{
    Task<TbmlPriceBenchmark?> GetByHsCodeAsync(string hsCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TbmlPriceBenchmark>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(TbmlPriceBenchmark benchmark, CancellationToken cancellationToken = default);
}
