// ARCHITECTURAL INTENT: Telegraphic Transfer Application Service Contract
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 3 (3.13) and Module 1 (1.6.5).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.TelegraphicTransfers.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface ITelegraphicTransferService
{
    Task<TelegraphicTransferResponseDto> IssueTtAsync(IssueTtRequest request, CancellationToken cancellationToken = default);
    Task<TelegraphicTransferResponseDto> TransmitAdviceAsync(string ttId, TransmitTtAdviceRequest request, CancellationToken cancellationToken = default);
    Task<TelegraphicTransferResponseDto> LogTelephonicCallbackAsync(string ttId, LogTelephonicCallbackRequest request, CancellationToken cancellationToken = default);
    Task<TelegraphicTransferResponseDto> RespondAndPayTtAsync(string ttId, RespondAndPayTtRequest request, CancellationToken cancellationToken = default);
    Task<TelegraphicTransferResponseDto> CancelTtAsync(string ttId, CancelTtRequest request, CancellationToken cancellationToken = default);
    Task<TelegraphicTransferResponseDto?> GetByIdAsync(string ttId, CancellationToken cancellationToken = default);
    Task<TelegraphicTransferResponseDto?> GetByTtNumberAsync(string ttNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelegraphicTransferResponseDto>> GetOutstandingTtAsync(string? branchCode, CancellationToken cancellationToken = default);
    Task<UnrespondedTtAgingReportDto> GetUnrespondedTtAgingReportAsync(string? branchCode, DateOnly asOfDate, CancellationToken cancellationToken = default);
}
