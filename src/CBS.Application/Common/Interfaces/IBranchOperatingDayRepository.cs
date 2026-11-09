using CBS.Domain.GeneralLedger;

namespace CBS.Application.Common.Interfaces;

public interface IBranchOperatingDayRepository
{
    Task<BranchOperatingDay?> GetByBranchCodeAsync(string branchCode, CancellationToken cancellationToken = default);
    Task AddAsync(BranchOperatingDay operatingDay, CancellationToken cancellationToken = default);
    Task UpdateAsync(BranchOperatingDay operatingDay, CancellationToken cancellationToken = default);
    Task<int> GetPendingVouchersCountAsync(string branchCode, CancellationToken cancellationToken = default);
    Task SetPendingVouchersCountAsync(string branchCode, int count, CancellationToken cancellationToken = default);
}
