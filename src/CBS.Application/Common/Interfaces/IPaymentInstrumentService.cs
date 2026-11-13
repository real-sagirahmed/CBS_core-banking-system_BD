// ARCHITECTURAL INTENT: Payment Instrument Application Service Contract
// adhering to Bangladesh Bank BRPD Circular No. 12 (1.5.6, 3.11, 3.12).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.PaymentInstruments.DTOs;
using CBS.Domain.Enums;

namespace CBS.Application.Common.Interfaces;

public interface IPaymentInstrumentService
{
    Task<PaymentInstrumentStockBookDto> AllocateStockBookAsync(AllocateStockBookRequest request, CancellationToken cancellationToken = default);
    Task<PaymentInstrumentResponseDto> IssuePayOrderAsync(IssuePayOrderRequest request, CancellationToken cancellationToken = default);
    Task<PaymentInstrumentResponseDto> IssueDemandDraftAsync(IssueDemandDraftRequest request, CancellationToken cancellationToken = default);
    Task<PaymentInstrumentResponseDto> EncashInstrumentAsync(string instrumentId, EncashPaymentInstrumentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentInstrumentResponseDto> CancelInstrumentAsync(string instrumentId, CancelPaymentInstrumentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentInstrumentResponseDto> IssueDuplicateAsync(IssueDuplicateInstrumentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentInstrumentResponseDto?> GetInstrumentByIdAsync(string instrumentId, CancellationToken cancellationToken = default);
    Task<PaymentInstrumentResponseDto?> GetInstrumentByNumberAsync(string instrumentNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentInstrumentResponseDto>> GetOutstandingInstrumentsAsync(string? branchCode, PaymentInstrumentType? type, CancellationToken cancellationToken = default);
    Task<UnpresentedAgingReportDto> GetUnpresentedAgingReportAsync(string? branchCode, DateOnly asOfDate, CancellationToken cancellationToken = default);
}
