using CBS.Application.BranchCash;
using CBS.Domain.BranchCash;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfSecurityDepositReceiptRepository : ISecurityDepositReceiptRepository
{
    private readonly CbsDbContext _dbContext;

    public EfSecurityDepositReceiptRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<SecurityDepositReceipt?> GetBySdrNumberAsync(string sdrNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SecurityDepositReceipts
            .FirstOrDefaultAsync(s => s.SdrNumber == sdrNumber.Trim().ToUpperInvariant(), cancellationToken);
    }

    public async Task<SecurityDepositReceipt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SecurityDepositReceipts
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SecurityDepositReceipt>> GetByTenderNumberAsync(string tenderNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SecurityDepositReceipts
            .Where(s => s.TenderOrRfpNumber == tenderNumber.Trim())
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SecurityDepositReceipt sdr, CancellationToken cancellationToken = default)
    {
        await _dbContext.SecurityDepositReceipts.AddAsync(sdr, cancellationToken);
    }

    public Task UpdateAsync(SecurityDepositReceipt sdr, CancellationToken cancellationToken = default)
    {
        _dbContext.SecurityDepositReceipts.Update(sdr);
        return Task.CompletedTask;
    }

    public async Task AddStopPaymentJournalAsync(RemittanceStopPaymentJournal journal, CancellationToken cancellationToken = default)
    {
        await _dbContext.RemittanceStopPaymentJournals.AddAsync(journal, cancellationToken);
    }

    public async Task<IReadOnlyList<RemittanceStopPaymentJournal>> GetJournalsByInstrumentAsync(string instrumentNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RemittanceStopPaymentJournals
            .Where(j => j.InstrumentNumber == instrumentNumber.Trim().ToUpperInvariant())
            .OrderByDescending(j => j.ActionDateUtc)
            .ToListAsync(cancellationToken);
    }
}
