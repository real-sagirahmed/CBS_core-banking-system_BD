using CBS.Domain.TradeFinance;

namespace CBS.Application.Common.Interfaces;

public interface ITbmlAlertRepository
{
    Task<TbmlAlertRecord?> GetByIdAsync(Guid alertId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TbmlAlertRecord>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TbmlAlertRecord>> GetAlertsByReferenceAsync(string referenceNumber, CancellationToken cancellationToken = default);
    Task AddAsync(TbmlAlertRecord alert, CancellationToken cancellationToken = default);
    Task UpdateAsync(TbmlAlertRecord alert, CancellationToken cancellationToken = default);
}
