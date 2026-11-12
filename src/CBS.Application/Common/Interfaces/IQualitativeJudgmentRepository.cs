// ARCHITECTURAL INTENT: Repository contract for Qualitative Judgment (QJ) classification overrides.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 & 4.2.

using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface IQualitativeJudgmentRepository
{
    Task<QualitativeJudgmentRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QualitativeJudgmentRecord?> GetActiveByLoanAccountNumberAsync(AccountNumber loanAccountNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QualitativeJudgmentRecord>> GetHistoryByLoanAccountNumberAsync(AccountNumber loanAccountNumber, CancellationToken cancellationToken = default);
    Task AddAsync(QualitativeJudgmentRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(QualitativeJudgmentRecord record, CancellationToken cancellationToken = default);
}
