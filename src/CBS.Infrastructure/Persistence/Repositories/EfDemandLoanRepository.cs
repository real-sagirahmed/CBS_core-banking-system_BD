using CBS.Application.Lending.Demand;
using CBS.Domain.Enums;
using CBS.Domain.Lending.Demand;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfDemandLoanRepository : IDemandLoanRepository
{
    private readonly CbsDbContext _dbContext;

    public EfDemandLoanRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<DemandLoanFacility?> GetByAccountNumberAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DemandLoanFacilities
            .FirstOrDefaultAsync(d => d.Id == accountNumber, cancellationToken);
    }

    public async Task<DemandLoanFacility?> GetByLoanReferenceAsync(string loanReference, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DemandLoanFacilities
            .FirstOrDefaultAsync(d => d.LoanReferenceNumber == loanReference, cancellationToken);
    }

    public async Task<IReadOnlyList<DemandLoanFacility>> GetOverdueLoansAsync(string? branchCode = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DemandLoanFacilities
            .Where(d => d.Status == DemandLoanStatus.Overdue);

        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            query = query.Where(d => d.BranchCode == branchCode);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DemandLoanFacility facility, CancellationToken cancellationToken = default)
    {
        await _dbContext.DemandLoanFacilities.AddAsync(facility, cancellationToken);
    }

    public Task UpdateAsync(DemandLoanFacility facility, CancellationToken cancellationToken = default)
    {
        _dbContext.DemandLoanFacilities.Update(facility);
        return Task.CompletedTask;
    }
}
