using CBS.Application.TradeFinance;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfBankGuaranteeRepository : IBankGuaranteeRepository
{
    private readonly CbsDbContext _dbContext;

    public EfBankGuaranteeRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<BankGuarantee?> GetByBgNumberAsync(string bgNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BankGuarantees
            .FirstOrDefaultAsync(b => b.BgNumber == bgNumber.Trim().ToUpperInvariant(), cancellationToken);
    }

    public async Task<BankGuarantee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BankGuarantees
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<BankGuarantee>> GetByApplicantCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BankGuarantees
            .Where(b => b.ApplicantCif == cif)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BankGuarantee bg, CancellationToken cancellationToken = default)
    {
        await _dbContext.BankGuarantees.AddAsync(bg, cancellationToken);
    }

    public Task UpdateAsync(BankGuarantee bg, CancellationToken cancellationToken = default)
    {
        _dbContext.BankGuarantees.Update(bg);
        return Task.CompletedTask;
    }
}
