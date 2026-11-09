using System.Collections.Concurrent;
using CBS.Application.Common.Interfaces;
using CBS.Domain.BranchCash;

namespace CBS.Infrastructure.Persistence;

public class InMemoryRemittanceInstrumentRepository : IRemittanceInstrumentRepository
{
    private readonly ConcurrentDictionary<string, RemittanceInstrument> _instruments = new();

    public Task<RemittanceInstrument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var inst = _instruments.Values.FirstOrDefault(i => i.Id == id);
        return Task.FromResult(inst);
    }

    public Task<RemittanceInstrument?> GetByInstrumentNumberAsync(string instrumentNumber, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrumentNumber);
        _instruments.TryGetValue(instrumentNumber.Trim().ToUpperInvariant(), out var inst);
        return Task.FromResult(inst);
    }

    public Task AddAsync(RemittanceInstrument instrument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        string key = instrument.InstrumentNumber.ToUpperInvariant();
        if (!_instruments.TryAdd(key, instrument))
        {
            throw new InvalidOperationException($"Remittance instrument '{instrument.InstrumentNumber}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(RemittanceInstrument instrument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        string key = instrument.InstrumentNumber.ToUpperInvariant();
        _instruments[key] = instrument;
        return Task.CompletedTask;
    }
}
