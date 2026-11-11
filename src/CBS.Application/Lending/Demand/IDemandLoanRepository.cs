using CBS.Domain.Lending.Demand;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Lending.Demand;

public interface IDemandLoanRepository
{
    Task<DemandLoanFacility?> GetByAccountNumberAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default);
    Task<DemandLoanFacility?> GetByLoanReferenceAsync(string loanReference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandLoanFacility>> GetOverdueLoansAsync(string? branchCode = null, CancellationToken cancellationToken = default);
    Task AddAsync(DemandLoanFacility facility, CancellationToken cancellationToken = default);
    Task UpdateAsync(DemandLoanFacility facility, CancellationToken cancellationToken = default);
}
