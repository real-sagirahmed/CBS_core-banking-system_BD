using CBS.Application.Common.Interfaces;
using CBS.Domain.Deposits;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfDpsRepository : IDpsRepository
{
    private readonly CbsDbContext _dbContext;

    public EfDpsRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<DpsAccount?> GetByIdAsync(string dpsAccountId, CancellationToken ct = default)
    {
        return await _dbContext.DpsAccounts
            .Include(d => d.Installments)
            .FirstOrDefaultAsync(d => d.Id == dpsAccountId, ct);
    }

    public async Task<DpsAccount?> GetByDpsAccountNumberAsync(string dpsAccountNumber, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(dpsAccountNumber);
        return await _dbContext.DpsAccounts
            .Include(d => d.Installments)
            .FirstOrDefaultAsync(d => d.DpsAccountNumber == acc, ct);
    }

    public async Task<IEnumerable<DpsAccount>> GetByLinkedCasaAsync(string linkedCasaAccountNumber, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(linkedCasaAccountNumber);
        return await _dbContext.DpsAccounts
            .Include(d => d.Installments)
            .Where(d => d.LinkedCasaAccountNumber == acc)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<DpsAccount>> GetActiveAccountsDueForSweepAsync(DateOnly date, CancellationToken ct = default)
    {
        return await _dbContext.DpsAccounts
            .Include(d => d.Installments)
            .Where(d => (d.Status == DpsStatus.Active || d.Status == DpsStatus.LienMarked) &&
                        d.Installments.Any(i => i.Status == DpsInstallmentStatus.Pending && i.DueDate <= date))
            .ToListAsync(ct);
    }

    public async Task AddAsync(DpsAccount account, CancellationToken ct = default)
    {
        await _dbContext.DpsAccounts.AddAsync(account, ct);
    }

    public Task UpdateAsync(DpsAccount account, CancellationToken ct = default)
    {
        _dbContext.DpsAccounts.Update(account);
        return Task.CompletedTask;
    }
}
