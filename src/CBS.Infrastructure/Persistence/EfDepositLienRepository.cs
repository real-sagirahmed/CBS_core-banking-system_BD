using CBS.Application.Common.Interfaces;
using CBS.Domain.Deposits;
using CBS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfDepositLienRepository : IDepositLienRepository
{
    private readonly CbsDbContext _dbContext;

    public EfDepositLienRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<DepositLienFacility?> GetByIdAsync(string lienId, CancellationToken ct = default)
    {
        return await _dbContext.DepositLiens
            .FirstOrDefaultAsync(l => l.Id == lienId, ct);
    }

    public async Task<DepositLienFacility?> GetActiveLienByDepositAccountAsync(string depositAccountId, CancellationToken ct = default)
    {
        return await _dbContext.DepositLiens
            .FirstOrDefaultAsync(l => l.DepositAccountId == depositAccountId && l.Status == DepositLienStatus.Active, ct);
    }

    public async Task<IEnumerable<DepositLienFacility>> GetLiensByLoanAccountAsync(string loanAccountId, CancellationToken ct = default)
    {
        return await _dbContext.DepositLiens
            .Where(l => l.LoanAccountId == loanAccountId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(DepositLienFacility lien, CancellationToken ct = default)
    {
        await _dbContext.DepositLiens.AddAsync(lien, ct);
    }

    public Task UpdateAsync(DepositLienFacility lien, CancellationToken ct = default)
    {
        _dbContext.DepositLiens.Update(lien);
        return Task.CompletedTask;
    }
}
