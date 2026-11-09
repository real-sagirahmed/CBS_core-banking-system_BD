using System.Collections.Concurrent;
using CBS.Application.Common.Interfaces;
using CBS.Domain.PaymentRails;

namespace CBS.Infrastructure.Persistence;

public class InMemoryChequeClearingRepository : IChequeClearingRepository
{
    private readonly ConcurrentDictionary<Guid, ChequeClearingItem> _items = new();

    public Task<ChequeClearingItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _items.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task<IEnumerable<ChequeClearingItem>> GetByBatchIdAsync(string batchId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batchId);
        var matches = _items.Values.Where(i => i.BatchId.Equals(batchId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(matches);
    }

    public Task AddAsync(ChequeClearingItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!_items.TryAdd(item.Id, item))
        {
            throw new InvalidOperationException($"Cheque clearing item '{item.Id}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ChequeClearingItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items[item.Id] = item;
        return Task.CompletedTask;
    }
}
