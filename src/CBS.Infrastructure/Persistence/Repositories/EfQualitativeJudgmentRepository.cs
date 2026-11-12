// ARCHITECTURAL INTENT: EF Core repository implementation for QualitativeJudgmentRecord.
// Conforms to Bangladesh Bank BRPD Circular 12 (4.1.11-iii, 4.2.27-iii, Rule 5.2).

using CBS.Application.Common.Interfaces;
using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfQualitativeJudgmentRepository : IQualitativeJudgmentRepository
{
    private readonly CbsDbContext _dbContext;

    public EfQualitativeJudgmentRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<QualitativeJudgmentRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QualitativeJudgments
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task<QualitativeJudgmentRecord?> GetActiveByLoanAccountNumberAsync(AccountNumber loanAccountNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QualitativeJudgments
            .FirstOrDefaultAsync(q => q.LoanAccountNumber == loanAccountNumber && q.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<QualitativeJudgmentRecord>> GetHistoryByLoanAccountNumberAsync(AccountNumber loanAccountNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QualitativeJudgments
            .Where(q => q.LoanAccountNumber == loanAccountNumber)
            .OrderByDescending(q => q.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(QualitativeJudgmentRecord record, CancellationToken cancellationToken = default)
    {
        await _dbContext.QualitativeJudgments.AddAsync(record, cancellationToken);
    }

    public Task UpdateAsync(QualitativeJudgmentRecord record, CancellationToken cancellationToken = default)
    {
        _dbContext.QualitativeJudgments.Update(record);
        return Task.CompletedTask;
    }
}
