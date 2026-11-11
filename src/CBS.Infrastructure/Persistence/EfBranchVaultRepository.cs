using CBS.Application.BranchCash;
using CBS.Domain.BranchCash;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfBranchVaultRepository : IBranchVaultRepository
{
    private readonly CbsDbContext _dbContext;

    public EfBranchVaultRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<BranchVault?> GetByBranchCodeAsync(string branchCode, CancellationToken ct = default)
    {
        return await _dbContext.BranchVaults
            .FirstOrDefaultAsync(v => v.BranchCode == branchCode, ct);
    }

    public async Task AddAsync(BranchVault vault, CancellationToken ct = default)
    {
        await _dbContext.BranchVaults.AddAsync(vault, ct);
    }

    public Task UpdateAsync(BranchVault vault, CancellationToken ct = default)
    {
        var entry = _dbContext.Entry(vault);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.BranchVaults.Update(vault);
        }
        return Task.CompletedTask;
    }
}
