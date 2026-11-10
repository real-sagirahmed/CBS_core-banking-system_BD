using CBS.Application.Common.Interfaces;
using CBS.Domain.BranchCash;
using CBS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfSecurityStationeryRepository : ISecurityStationeryRepository
{
    private readonly CbsDbContext _dbContext;

    public EfSecurityStationeryRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<SecurityStationeryInventory?> GetActiveInventoryAsync(string branchCode, RemittanceInstrumentType type, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SecurityStationeryInventories
            .FirstOrDefaultAsync(i =>
                i.BranchCode == branchCode &&
                i.InstrumentType == type &&
                i.IsActive &&
                i.CurrentLeaf <= i.EndLeaf, cancellationToken);
    }

    public async Task AddAsync(SecurityStationeryInventory inventory, CancellationToken cancellationToken = default)
    {
        await _dbContext.SecurityStationeryInventories.AddAsync(inventory, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SecurityStationeryInventory inventory, CancellationToken cancellationToken = default)
    {
        _dbContext.SecurityStationeryInventories.Update(inventory);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
