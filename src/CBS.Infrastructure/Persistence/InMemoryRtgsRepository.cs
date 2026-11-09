using System.Collections.Concurrent;
using CBS.Application.Common.Interfaces;
using CBS.Domain.PaymentRails;

namespace CBS.Infrastructure.Persistence;

public class InMemoryRtgsRepository : IRtgsRepository
{
    private readonly ConcurrentDictionary<string, RtgsTransaction> _transactions = new();

    public Task<RtgsTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = _transactions.Values.FirstOrDefault(t => t.Id == id);
        return Task.FromResult(match);
    }

    public Task<RtgsTransaction?> GetByEndToEndIdAsync(string endToEndId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endToEndId);
        _transactions.TryGetValue(endToEndId.Trim().ToUpperInvariant(), out var tx);
        return Task.FromResult(tx);
    }

    public Task AddAsync(RtgsTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        string key = transaction.EndToEndId.ToUpperInvariant();
        if (!_transactions.TryAdd(key, transaction))
        {
            throw new InvalidOperationException($"RTGS transaction '{transaction.EndToEndId}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(RtgsTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        string key = transaction.EndToEndId.ToUpperInvariant();
        _transactions[key] = transaction;
        return Task.CompletedTask;
    }
}
