using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.TermDeposits;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfTermDepositRepository : ITermDepositRepository
{
    private readonly CbsDbContext _dbContext;

    public EfTermDepositRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<TermDepositContract?> GetContractByIdAsync(string contractId, CancellationToken ct = default)
    {
        return await _dbContext.TermDepositContracts
            .FirstOrDefaultAsync(t => t.Id == contractId, ct);
    }

    public async Task<TermDepositContract?> GetByFdrAccountNumberAsync(string fdrAccountNumber, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(fdrAccountNumber);
        return await _dbContext.TermDepositContracts
            .FirstOrDefaultAsync(t => t.FdrAccountNumber == acc, ct);
    }

    public async Task<IEnumerable<TermDepositContract>> GetByOperativeAccountAsync(string operativeAccountNumber, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(operativeAccountNumber);
        return await _dbContext.TermDepositContracts
            .Where(t => t.OperativeAccountNumber == acc)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<TermDepositContract>> GetMaturingContractsAsync(DateOnly maturityDate, CancellationToken ct = default)
    {
        return await _dbContext.TermDepositContracts
            .Where(t => (t.Status == TermDepositStatus.Active || t.Status == TermDepositStatus.LienMarked) && t.MaturityDate <= maturityDate)
            .ToListAsync(ct);
    }

    public async Task AddAsync(TermDepositContract contract, CancellationToken ct = default)
    {
        await _dbContext.TermDepositContracts.AddAsync(contract, ct);
    }

    public Task UpdateAsync(TermDepositContract contract, CancellationToken ct = default)
    {
        _dbContext.TermDepositContracts.Update(contract);
        return Task.CompletedTask;
    }
}
