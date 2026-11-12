// ARCHITECTURAL INTENT: Service contract for CRM Regulatory Compliance Suite
// (Customer Risk Grading, Sanction Screening, FATCA US Indicia, and Borrower ICRR Scorecard)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 8, BRPD 03/2019, and e-KYC Guidelines.

using CBS.Application.Customers.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface ICrmComplianceService
{
    // Customer Risk Grading (CRG)
    Task<CustomerRiskAssessmentDto> EvaluateCustomerRiskAsync(EvaluateCustomerRiskCommand command, CancellationToken cancellationToken = default);
    Task<CustomerRiskAssessmentDto> ApproveSeniorManagementAsync(ApproveSeniorManagementCommand command, CancellationToken cancellationToken = default);
    Task<CustomerRiskAssessmentDto?> GetCustomerRiskAssessmentAsync(string cif, CancellationToken cancellationToken = default);

    // Sanction Screening
    Task<SanctionScreeningResultDto> ScreenSanctionAsync(ScreenSanctionCommand command, CancellationToken cancellationToken = default);
    Task<SanctionScreeningResultDto> InvestigateSanctionAsync(InvestigateSanctionCommand command, CancellationToken cancellationToken = default);
    Task<SanctionScreeningResultDto> ResolveSanctionCheckerAsync(ResolveSanctionCheckerCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SanctionScreeningResultDto>> GetPendingSanctionsAsync(CancellationToken cancellationToken = default);

    // FATCA Compliance
    Task<FatcaComplianceProfileDto> EvaluateFatcaIndiciaAsync(EvaluateFatcaIndiciaCommand command, CancellationToken cancellationToken = default);
    Task<FatcaComplianceProfileDto> SubmitFatcaW9Async(SubmitFatcaW9Command command, CancellationToken cancellationToken = default);
    Task<FatcaComplianceProfileDto> SubmitFatcaW8BenAsync(SubmitFatcaW8BenCommand command, CancellationToken cancellationToken = default);
    Task<FatcaComplianceProfileDto?> GetFatcaProfileAsync(string cif, CancellationToken cancellationToken = default);

    // Borrower ICRR Scorecard
    Task<BorrowerIcrrAssessmentDto> EvaluateBorrowerIcrrAsync(EvaluateBorrowerIcrrCommand command, CancellationToken cancellationToken = default);
    Task<BorrowerIcrrAssessmentDto> ApproveIcrrByBoardAsync(ApproveIcrrByBoardCommand command, CancellationToken cancellationToken = default);
    Task<BorrowerIcrrAssessmentDto?> GetLatestBorrowerIcrrAsync(string cif, CancellationToken cancellationToken = default);
    Task VerifyBorrowerSanctionEligibilityAsync(string cif, CancellationToken cancellationToken = default);
}
