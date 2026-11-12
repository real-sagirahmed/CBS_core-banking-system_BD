using CBS.Application.Common.Interfaces;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfAccountRestraintRepository : IAccountRestraintRepository
{
    private readonly CbsDbContext _dbContext;

    public EfAccountRestraintRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AccountRestraint?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _dbContext.AccountRestraints
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IEnumerable<AccountRestraint>> GetByAccountNumberAsync(AccountNumber accountNumber, CancellationToken ct = default)
    {
        return await _dbContext.AccountRestraints
            .Where(r => r.AccountNumber == accountNumber)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<AccountRestraint>> GetActiveRestraintsByAccountNumberAsync(AccountNumber accountNumber, CancellationToken ct = default)
    {
        return await _dbContext.AccountRestraints
            .Where(r => r.AccountNumber == accountNumber && r.Status == RestraintStatus.Active)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(AccountRestraint restraint, CancellationToken ct = default)
    {
        await _dbContext.AccountRestraints.AddAsync(restraint, ct);
    }

    public Task UpdateAsync(AccountRestraint restraint, CancellationToken ct = default)
    {
        _dbContext.AccountRestraints.Update(restraint);
        return Task.CompletedTask;
    }
}
