using CBS.Domain.GeneralLedger;

namespace CBS.Application.Common.Interfaces;

public interface IChartOfAccountsRepository
{
    Task<ChartOfAccount?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChartOfAccount>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ChartOfAccount account, CancellationToken cancellationToken = default);
    Task SeedDefaultAccountsAsync(CancellationToken cancellationToken = default);
}
