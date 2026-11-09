using CBS.Domain.BranchCash;

namespace CBS.Application.Common.Interfaces;

public interface ITellerDrawerRepository
{
    Task<TellerDrawer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TellerDrawer?> GetActiveDrawerByCashierAsync(string cashierUserId, DateOnly operatingDate, CancellationToken cancellationToken = default);
    Task AddAsync(TellerDrawer drawer, CancellationToken cancellationToken = default);
    Task UpdateAsync(TellerDrawer drawer, CancellationToken cancellationToken = default);
}
