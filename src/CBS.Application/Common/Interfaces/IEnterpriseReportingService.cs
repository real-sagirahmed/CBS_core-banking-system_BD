using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Reporting.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface IEnterpriseReportingService
{
    // 1. Wage Earners' Remittance 2.5% Cash Incentive
    Task<RemittanceIncentiveResponseDto> DisburseRemittanceIncentiveAsync(DisburseRemittanceIncentiveRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<RemittanceIncentiveResponseDto>> GetPendingIncentiveClaimsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RemittanceIncentiveResponseDto>> SubmitIncentiveClaimsAsync(SubmitRemittanceIncentiveBatchClaimRequest request, CancellationToken ct = default);
    Task<RemittanceIncentiveResponseDto> RecordReimbursementAsync(string incentiveId, RecordCentralBankReimbursementRequest request, CancellationToken ct = default);

    // 2. Branch Daily Clean Cash & Proofing
    Task<DailyCleanCashResponseDto> GenerateDailyCleanCashAsync(GenerateDailyCleanCashRequest request, CancellationToken ct = default);
    Task<DailyCleanCashResponseDto> CertifyDailyCleanCashAsync(string sheetId, CertifyDailyCleanCashRequest request, CancellationToken ct = default);
    Task<DailyCleanCashResponseDto> GetDailyCleanCashAsync(string sheetId, CancellationToken ct = default);

    // 3. Statutory Regulatory Returns
    Task<LargeLoanPortfolioResponseDto> GenerateLargeLoanExposureReportAsync(decimal capitalBaseBdt, CancellationToken ct = default);
    Task<NplClassificationSummaryDto> GenerateNplClassificationSummaryAsync(DateOnly asOfDate, CancellationToken ct = default);
}
