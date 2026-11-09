using System.Collections.Concurrent;
using CBS.Application.Common.Interfaces;
using CBS.Domain.PaymentRails;

namespace CBS.Infrastructure.Persistence;

public class InMemoryBeftnRepository : IBeftnRepository
{
    private readonly ConcurrentDictionary<Guid, BeftnBatchItem> _items = new();

    public Task<BeftnBatchItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _items.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task<IEnumerable<BeftnBatchItem>> GetByBatchIdAsync(string batchId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batchId);
        var matches = _items.Values.Where(i => i.BatchId.Equals(batchId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(matches);
    }

    public Task AddAsync(BeftnBatchItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!_items.TryAdd(item.Id, item))
        {
            throw new InvalidOperationException($"BEFTN item '{item.Id}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(BeftnBatchItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items[item.Id] = item;
        return Task.CompletedTask;
    }
}
