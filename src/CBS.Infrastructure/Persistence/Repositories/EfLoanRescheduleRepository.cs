// ARCHITECTURAL INTENT: EF Core repository implementation for LoanRescheduleRecord.
// Conforms to Bangladesh Bank BRPD Circular 12 (4.1.12, 4.2.28) and BRPD 16/2020 & 04/2023.

using CBS.Application.Common.Interfaces;
using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfLoanRescheduleRepository : ILoanRescheduleRepository
{
    private readonly CbsDbContext _dbContext;

    public EfLoanRescheduleRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<LoanRescheduleRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanReschedules
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<LoanRescheduleRecord>> GetByLoanAccountNumberAsync(AccountNumber loanAccountNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanReschedules
            .Where(r => r.LoanAccountNumber == loanAccountNumber)
            .OrderBy(r => r.RescheduleIteration)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(LoanRescheduleRecord record, CancellationToken cancellationToken = default)
    {
        await _dbContext.LoanReschedules.AddAsync(record, cancellationToken);
    }

    public Task UpdateAsync(LoanRescheduleRecord record, CancellationToken cancellationToken = default)
    {
        _dbContext.LoanReschedules.Update(record);
        return Task.CompletedTask;
    }
}
