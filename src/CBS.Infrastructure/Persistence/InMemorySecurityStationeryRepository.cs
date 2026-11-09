using System.Collections.Concurrent;
using CBS.Application.Common.Interfaces;
using CBS.Domain.BranchCash;
using CBS.Domain.Enums;

namespace CBS.Infrastructure.Persistence;

public class InMemorySecurityStationeryRepository : ISecurityStationeryRepository
{
    private readonly ConcurrentDictionary<Guid, SecurityStationeryInventory> _inventories = new();

    public Task<SecurityStationeryInventory?> GetActiveInventoryAsync(string branchCode, RemittanceInstrumentType type, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(branchCode);
        var inv = _inventories.Values.FirstOrDefault(i =>
            i.BranchCode.Equals(branchCode, StringComparison.OrdinalIgnoreCase) &&
            i.InstrumentType == type &&
            i.IsActive &&
            i.CurrentLeaf <= i.EndLeaf);

        return Task.FromResult(inv);
    }

    public Task AddAsync(SecurityStationeryInventory inventory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (!_inventories.TryAdd(inventory.Id, inventory))
        {
            throw new InvalidOperationException($"Security stationery inventory '{inventory.Id}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SecurityStationeryInventory inventory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        _inventories[inventory.Id] = inventory;
        return Task.CompletedTask;
    }
}
