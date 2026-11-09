using System.Collections.Concurrent;
using CBS.Application.Common.Interfaces;
using CBS.Domain.GeneralLedger;

namespace CBS.Infrastructure.Persistence;

public class InMemoryBranchOperatingDayRepository : IBranchOperatingDayRepository
{
    private readonly ConcurrentDictionary<string, BranchOperatingDay> _operatingDays = new();
    private readonly ConcurrentDictionary<string, int> _pendingVouchers = new();

    public Task<BranchOperatingDay?> GetByBranchCodeAsync(string branchCode, CancellationToken cancellationToken = default)
    {
        _operatingDays.TryGetValue(branchCode, out var opDay);
        return Task.FromResult(opDay);
    }

    public Task AddAsync(BranchOperatingDay operatingDay, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operatingDay);
        _operatingDays[operatingDay.Id] = operatingDay;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(BranchOperatingDay operatingDay, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operatingDay);
        _operatingDays[operatingDay.Id] = operatingDay;
        return Task.CompletedTask;
    }

    public Task<int> GetPendingVouchersCountAsync(string branchCode, CancellationToken cancellationToken = default)
    {
        _pendingVouchers.TryGetValue(branchCode, out int count);
        return Task.FromResult(count);
    }

    public Task SetPendingVouchersCountAsync(string branchCode, int count, CancellationToken cancellationToken = default)
    {
        _pendingVouchers[branchCode] = count;
        return Task.CompletedTask;
    }
}
