// ARCHITECTURAL INTENT: Continuous Loan Application Service Contract
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 and 3.9.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Lending.Continuous.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface IContinuousLoanService
{
    Task<ContinuousLoanResponseDto> OpenFacilityAsync(OpenContinuousLoanRequest request, CancellationToken cancellationToken = default);
    Task<StockStatementResponseDto> SubmitStockStatementAsync(string accountNumber, SubmitStockStatementRequest request, CancellationToken cancellationToken = default);
    Task<GodownInspectionResponseDto> RecordGodownInspectionAsync(string accountNumber, RecordGodownInspectionRequest request, CancellationToken cancellationToken = default);
    Task<TemporaryOverdraftResponseDto> GrantTemporaryOverdraftAsync(string accountNumber, GrantTemporaryOverdraftRequest request, CancellationToken cancellationToken = default);
    Task<ContinuousLoanResponseDto> WithdrawAsync(string accountNumber, ContinuousLoanWithdrawalRequest request, CancellationToken cancellationToken = default);
    Task<ContinuousLoanResponseDto> DepositAsync(string accountNumber, ContinuousLoanDepositRequest request, CancellationToken cancellationToken = default);
    Task<QuarterlyCapitalizationResponseDto> CapitalizeInterestAsync(string accountNumber, QuarterlyCapitalizationRequest request, CancellationToken cancellationToken = default);
    Task<ContinuousLoanRenewalResponseDto> RenewFacilityAsync(string accountNumber, RenewContinuousLoanRequest request, CancellationToken cancellationToken = default);
    Task<ContinuousLoanResponseDto?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
    Task<DrawingPowerVarianceReportDto> GetDrawingPowerVarianceReportAsync(DateOnly asOfDate, string? branchCode, CancellationToken cancellationToken = default);
    Task<ExpiredContinuousLoanReportDto> GetExpiredUnrenewedReportAsync(DateOnly asOfDate, string? branchCode, CancellationToken cancellationToken = default);
    Task<OverdueStockStatementReportDto> GetOverdueStockStatementsReportAsync(DateOnly asOfDate, int thresholdDays, string? branchCode, CancellationToken cancellationToken = default);
}
