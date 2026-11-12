// ARCHITECTURAL INTENT: EF Core repository implementation for LoanWriteOffRecord.
// Conforms to Bangladesh Bank BRPD Circular 01/2013 & 08/2023, and Banking Companies Act 1991 §49.

using CBS.Application.Common.Interfaces;
using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfLoanWriteOffRepository : ILoanWriteOffRepository
{
    private readonly CbsDbContext _dbContext;

    public EfLoanWriteOffRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<LoanWriteOffRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanWriteOffs
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<LoanWriteOffRecord?> GetByLoanAccountNumberAsync(AccountNumber loanAccountNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanWriteOffs
            .FirstOrDefaultAsync(w => w.LoanAccountNumber == loanAccountNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<LoanWriteOffRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanWriteOffs
            .OrderByDescending(w => w.WriteOffDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(LoanWriteOffRecord record, CancellationToken cancellationToken = default)
    {
        await _dbContext.LoanWriteOffs.AddAsync(record, cancellationToken);
    }

    public Task UpdateAsync(LoanWriteOffRecord record, CancellationToken cancellationToken = default)
    {
        _dbContext.LoanWriteOffs.Update(record);
        return Task.CompletedTask;
    }
}
