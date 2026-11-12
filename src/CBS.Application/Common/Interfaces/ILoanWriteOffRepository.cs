// ARCHITECTURAL INTENT: Repository contract for statutory loan write-offs and post-writeoff recoveries.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024), BRPD 01/2013, 08/2023 & Banking Companies Act 1991 §49.

using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface ILoanWriteOffRepository
{
    Task<LoanWriteOffRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LoanWriteOffRecord?> GetByLoanAccountNumberAsync(AccountNumber loanAccountNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoanWriteOffRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(LoanWriteOffRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(LoanWriteOffRecord record, CancellationToken cancellationToken = default);
}
