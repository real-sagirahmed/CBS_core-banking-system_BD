using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.BackValue.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface IBackValueGovernanceService
{
    Task<BackValueTransactionResponseDto> SubmitBackValueRequestAsync(SubmitBackValueTransactionRequest request, CancellationToken ct = default);
    Task<BackValueTransactionResponseDto> ApproveAndExecuteBackValueAsync(string requestId, ApproveBackValueTransactionRequest request, CancellationToken ct = default);
    Task<BackValueTransactionResponseDto> RejectBackValueRequestAsync(string requestId, RejectBackValueTransactionRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<BackValueTransactionResponseDto>> GetPendingBackValueRequestsAsync(CancellationToken ct = default);

    RetroactiveInterestCalculationResultDto SimulateRetroactiveInterest(SimulateRetroactiveInterestRequest request);
    Task<CascadeReversalResponseDto> ExecuteCascadeReversalAsync(ReverseTransactionCascadeRequest request, CancellationToken ct = default);
    Task<EodBackValueJournalReportDto> GetEodBackValueJournalReportAsync(DateOnly operatingDate, string? branchCode = null, CancellationToken ct = default);
}
