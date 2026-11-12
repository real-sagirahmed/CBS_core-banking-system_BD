// ARCHITECTURAL INTENT: Repository contract for statutory loan rescheduling history and tracking.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 & 4.2, BRPD 16/2020 & 04/2023.

using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface ILoanRescheduleRepository
{
    Task<LoanRescheduleRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoanRescheduleRecord>> GetByLoanAccountNumberAsync(AccountNumber loanAccountNumber, CancellationToken cancellationToken = default);
    Task AddAsync(LoanRescheduleRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(LoanRescheduleRecord record, CancellationToken cancellationToken = default);
}
