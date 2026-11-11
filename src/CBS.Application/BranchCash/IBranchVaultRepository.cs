using CBS.Domain.BranchCash;

namespace CBS.Application.BranchCash;

public interface IBranchVaultRepository
{
    Task<BranchVault?> GetByBranchCodeAsync(string branchCode, CancellationToken ct = default);
    Task AddAsync(BranchVault vault, CancellationToken ct = default);
    Task UpdateAsync(BranchVault vault, CancellationToken ct = default);
}
