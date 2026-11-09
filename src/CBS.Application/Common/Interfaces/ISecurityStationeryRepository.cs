using CBS.Domain.BranchCash;
using CBS.Domain.Enums;

namespace CBS.Application.Common.Interfaces;

public interface ISecurityStationeryRepository
{
    Task<SecurityStationeryInventory?> GetActiveInventoryAsync(string branchCode, RemittanceInstrumentType type, CancellationToken cancellationToken = default);
    Task AddAsync(SecurityStationeryInventory inventory, CancellationToken cancellationToken = default);
    Task UpdateAsync(SecurityStationeryInventory inventory, CancellationToken cancellationToken = default);
}
