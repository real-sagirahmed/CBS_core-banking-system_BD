// ARCHITECTURAL INTENT: Service contract for Treasury Management, FX Blotter, NOP,
// MTM Revaluation, Nostro Mirror Reconciliation, and Statutory Reserves (CRR/SLR).
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 6, DOS 05/2008, and MPD 03/2020.

using CBS.Application.Treasury.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface ITreasuryManagementService
{
    Task<FxBlotterSummaryDto> GetFxBlotterAsync(DateOnly? date = null, CancellationToken cancellationToken = default);
    Task<TreasuryLimitProfileDto> ConfigureLimitProfileAsync(ConfigureTreasuryLimitProfileCommand command, CancellationToken cancellationToken = default);
    Task<TreasuryLimitCheckResultDto> CheckAndEnforceLimitAsync(CheckTreasuryLimitCommand command, CancellationToken cancellationToken = default);
    Task<MtmRevaluationResultDto> ExecuteMtmRevaluationAsync(ExecuteMtmRevaluationCommand command, CancellationToken cancellationToken = default);
    Task<StatutoryReserveAssessmentDto> EvaluateStatutoryReservesAsync(EvaluateStatutoryReservesCommand command, CancellationToken cancellationToken = default);
    Task<StatutoryReserveAssessmentDto?> GetLatestStatutoryReserveAssessmentAsync(CancellationToken cancellationToken = default);
    Task IngestLocalNostroEntryAsync(IngestLocalNostroEntryCommand command, CancellationToken cancellationToken = default);
    Task<NostroReconciliationBatchResultDto> ReconcileNostroStatementsAsync(IngestNostroStatementCommand command, string matchedByUserId, CancellationToken cancellationToken = default);
    Task<NostroUnreconciledAgingReportDto> GetNostroUnreconciledAgingReportAsync(DateOnly? asOfDate = null, CancellationToken cancellationToken = default);
}
