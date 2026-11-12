using CBS.Application.Customers.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface ICustomerDocumentComplianceService
{
    Task<ExpiredDocumentScanSummaryDto> ScanAndApplyExpiredDocumentRestraintsAsync(ScanExpiredDocumentsRequest request, CancellationToken cancellationToken = default);
    Task<RenewDocumentResponseDto> RenewDocumentAndReleaseRestraintsAsync(Guid documentId, RenewDocumentRequest request, CancellationToken cancellationToken = default);
    Task<FatcaAssessmentDto> EvaluateFatcaComplianceAsync(FatcaComplianceEvaluationRequest request, CancellationToken cancellationToken = default);
}
