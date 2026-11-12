// ARCHITECTURAL INTENT: Service contract for automated asset classification, qualitative judgment,
// rescheduling lifecycle, and statutory write-off and recovery.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 & 4.2.

using CBS.Application.Lending.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface ILoanClassificationService
{
    Task<LoanClassificationBatchResultDto> EvaluatePortfolioClassificationAsync(string branchCode, DateTime evaluationDate, CancellationToken cancellationToken = default);
    Task<LoanPortfolioClassificationReportDto> GetPortfolioClassificationReportAsync(string? branchCode, CancellationToken cancellationToken = default);
    Task<QualitativeJudgmentResultDto> ApplyQualitativeJudgmentAsync(ApplyQualitativeJudgmentCommand command, CancellationToken cancellationToken = default);
    Task<QualitativeJudgmentResultDto> RevokeQualitativeJudgmentAsync(RevokeQualitativeJudgmentCommand command, CancellationToken cancellationToken = default);
    Task<RescheduleResultDto> RescheduleLoanAsync(RescheduleLoanCommand command, CancellationToken cancellationToken = default);
    Task<WriteOffResultDto> WriteOffLoanAsync(WriteOffLoanCommand command, CancellationToken cancellationToken = default);
    Task<WriteOffRecoveryResultDto> RecordWriteOffRecoveryAsync(RecordWriteOffRecoveryCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WrittenOffRegisterItemDto>> GetWrittenOffRegisterAsync(CancellationToken cancellationToken = default);
}
