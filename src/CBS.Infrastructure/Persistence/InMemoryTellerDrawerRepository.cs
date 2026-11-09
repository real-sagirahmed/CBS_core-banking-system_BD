using System.Collections.Concurrent;
using CBS.Application.Common.Interfaces;
using CBS.Domain.BranchCash;

namespace CBS.Infrastructure.Persistence;

public class InMemoryTellerDrawerRepository : ITellerDrawerRepository
{
    private readonly ConcurrentDictionary<Guid, TellerDrawer> _drawers = new();

    public Task<TellerDrawer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _drawers.TryGetValue(id, out var drawer);
        return Task.FromResult(drawer);
    }

    public Task<TellerDrawer?> GetActiveDrawerByCashierAsync(string cashierUserId, DateOnly operatingDate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cashierUserId);
        var drawer = _drawers.Values.FirstOrDefault(d =>
            d.CashierUserId.Equals(cashierUserId, StringComparison.OrdinalIgnoreCase) &&
            d.OperatingDate == operatingDate &&
            d.Status == Domain.Enums.TellerDrawerStatus.Active);

        return Task.FromResult(drawer);
    }

    public Task AddAsync(TellerDrawer drawer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(drawer);
        if (!_drawers.TryAdd(drawer.Id, drawer))
        {
            throw new InvalidOperationException($"Teller drawer '{drawer.Id}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TellerDrawer drawer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(drawer);
        _drawers[drawer.Id] = drawer;
        return Task.CompletedTask;
    }
}
