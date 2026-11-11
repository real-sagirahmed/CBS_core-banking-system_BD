using CBS.Domain.Reconciliation;

namespace CBS.Application.Reconciliation;

public interface IReconciliationAgingRepository
{
    Task<ReconciliationAgingItem?> GetByTraceNumberAsync(string traceNumber, CancellationToken cancellationToken = default);
    Task<ReconciliationAgingItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReconciliationAgingItem>> GetUnresolvedItemsAsync(string? channelType = null, CancellationToken cancellationToken = default);
    Task AddAsync(ReconciliationAgingItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(ReconciliationAgingItem item, CancellationToken cancellationToken = default);
}
