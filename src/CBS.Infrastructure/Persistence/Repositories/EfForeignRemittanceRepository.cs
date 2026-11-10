using CBS.Application.Remittance;
using CBS.Domain.Enums;
using CBS.Domain.Remittance;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfForeignRemittanceRepository : IForeignRemittanceRepository
{
    private readonly CbsDbContext _dbContext;

    public EfForeignRemittanceRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ForeignRemittanceTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ForeignRemittanceTransactions
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<ForeignRemittanceTransaction?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        string normalized = reference.Trim().ToUpperInvariant();
        return await _dbContext.ForeignRemittanceTransactions
            .FirstOrDefaultAsync(t => t.TransactionReference == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<ForeignRemittanceTransaction>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ForeignRemittanceTransactions
            .Where(t => t.Status == ForeignRemittanceStatus.PendingCheckerApproval)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForeignRemittanceTransaction>> GetUnclaimedIncentivesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ForeignRemittanceTransactions
            .Where(t => t.IncentiveStatus == GovernmentIncentiveStatus.DisbursedToBeneficiary && t.CentralBankClaimBatchId == null)
            .OrderBy(t => t.DisbursedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForeignRemittanceTransaction>> GetInwardRegisterAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ForeignRemittanceTransactions
            .Where(t => t.Direction == ForeignRemittanceDirection.Inward);

        if (fromUtc.HasValue)
            query = query.Where(t => t.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(t => t.CreatedAtUtc <= toUtc.Value);

        return await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ForeignRemittanceTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.ForeignRemittanceTransactions.AddAsync(transaction, cancellationToken);
    }

    public Task UpdateAsync(ForeignRemittanceTransaction transaction, CancellationToken cancellationToken = default)
    {
        _dbContext.ForeignRemittanceTransactions.Update(transaction);
        return Task.CompletedTask;
    }

    public async Task<GovernmentIncentiveClaimBatch?> GetClaimBatchByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.GovernmentIncentiveClaimBatches
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<GovernmentIncentiveClaimBatch?> GetClaimBatchByNumberAsync(string batchNumber, CancellationToken cancellationToken = default)
    {
        string normalized = batchNumber.Trim().ToUpperInvariant();
        return await _dbContext.GovernmentIncentiveClaimBatches
            .FirstOrDefaultAsync(b => b.BatchNumber == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<GovernmentIncentiveClaimBatch>> GetAllClaimBatchesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.GovernmentIncentiveClaimBatches
            .OrderByDescending(b => b.BatchedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddClaimBatchAsync(GovernmentIncentiveClaimBatch batch, CancellationToken cancellationToken = default)
    {
        await _dbContext.GovernmentIncentiveClaimBatches.AddAsync(batch, cancellationToken);
    }

    public Task UpdateClaimBatchAsync(GovernmentIncentiveClaimBatch batch, CancellationToken cancellationToken = default)
    {
        _dbContext.GovernmentIncentiveClaimBatches.Update(batch);
        return Task.CompletedTask;
    }
}
