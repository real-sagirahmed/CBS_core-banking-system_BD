// ARCHITECTURAL INTENT: DTOs and commands for Customer Risk Grading (CRG),
// Real-Time Sanction Screening, FATCA US Indicia Detection, and Borrower ICRR Scorecard
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 8, BRPD 03/2019, and e-KYC Guidelines.

using CBS.Domain.Enums;

namespace CBS.Application.Customers.DTOs;

// --- CUSTOMER RISK GRADING (CRG) ---
public sealed record EvaluateCustomerRiskCommand(
    string Cif,
    OnboardingChannel Channel,
    bool IsBorderOrHighRiskArea,
    bool IsPoliticallyExposedPerson,
    bool IsInfluentialPersonOrAssociate,
    int ProductRiskScore,
    int OccupationRiskScore,
    DateOnly? AssessmentDate,
    string EvaluatedByUserId,
    string? Notes = null
);

public sealed record ApproveSeniorManagementCommand(
    string Cif,
    string SeniorManagerUserId
);

public sealed record CustomerRiskAssessmentDto(
    Guid Id,
    string Cif,
    OnboardingChannel Channel,
    bool IsBorderOrHighRiskArea,
    bool IsPoliticallyExposedPerson,
    bool IsInfluentialPersonOrAssociate,
    int CompositeRiskScore,
    CustomerRiskGrade RiskGrade,
    bool IsEnhancedDueDiligenceRequired,
    DateOnly AssessmentDate,
    DateOnly NextReviewDate,
    string EvaluatedByUserId,
    string? SeniorManagementApprovedByUserId,
    bool IsApproved
);

// --- SANCTION SCREENING ---
public sealed record ScreenSanctionCommand(
    string? Cif,
    string FullName,
    DateOnly? DateOfBirth,
    string? Nationality
);

public sealed record InvestigateSanctionCommand(
    Guid ScreeningId,
    string ComplianceOfficerUserId,
    string InvestigationNotes,
    bool IsPotentialTruePositive
);

public sealed record ResolveSanctionCheckerCommand(
    Guid ScreeningId,
    string ComplianceHeadUserId,
    bool ConfirmAsSanctioned
);

public sealed record SanctionScreeningResultDto(
    Guid ScreeningId,
    string? Cif,
    string FullName,
    DateOnly? DateOfBirth,
    string? Nationality,
    SanctionWatchlistType WatchlistType,
    string? MatchedEntityName,
    string? MatchedEntityReference,
    decimal MatchConfidenceScore,
    SanctionMatchStatus Status,
    bool IsHardBlocked,
    string? InvestigatedByUserId,
    string? ApprovedByComplianceHeadUserId
);

// --- FATCA COMPLIANCE ---
public sealed record EvaluateFatcaIndiciaCommand(
    string Cif,
    bool HasUsCitizenshipOrGreenCard,
    bool HasUsPlaceOfBirth,
    bool HasUsAddress,
    bool HasUsPhoneNumber,
    bool HasStandingInstructionsToUsAccount,
    bool HasPowerOfAttorneyGrantedToUsAddress,
    bool HasInCareOfOrHoldMailAddress,
    DateOnly? EvaluationDate,
    string EvaluatedByUserId,
    string? Giin = null
);

public sealed record SubmitFatcaW9Command(
    string Cif,
    string UsTin,
    DateOnly SubmissionDate,
    string VerifiedByUserId
);

public sealed record SubmitFatcaW8BenCommand(
    string Cif,
    DateOnly SubmissionDate,
    string VerifiedByUserId
);

public sealed record FatcaComplianceProfileDto(
    Guid Id,
    string Cif,
    bool HasAnyUsIndicia,
    FatcaClassification Classification,
    string? UsTaxpayerIdentificationNumber,
    bool IsFormW9Submitted,
    DateOnly? FormW9SubmissionDate,
    bool IsFormW8BenSubmitted,
    DateOnly? FormW8BenSubmissionDate,
    string? GlobalIntermediaryIdentificationNumber,
    bool IsSubjectToWithholdingTax,
    DateOnly? CureDeadlineDate
);

// --- BORROWER ICRR SCORECARD ---
public sealed record EvaluateBorrowerIcrrCommand(
    string Cif,
    string BorrowerName,
    string IndustrySector,
    DateOnly FinancialStatementDate,
    DateOnly AssessmentDate,
    decimal LeverageScore,
    decimal LiquidityScore,
    decimal ProfitabilityScore,
    decimal CoverageScore,
    decimal BusinessRiskScore,
    decimal ManagementRiskScore,
    decimal SecurityRiskScore,
    string EvaluatedByUserId
);

public sealed record ApproveIcrrByBoardCommand(
    Guid AssessmentId,
    string BoardResolutionReference,
    DateOnly BoardResolutionDate,
    string BoardMitigationStrategy,
    string ApprovedByUserId
);

public sealed record BorrowerIcrrAssessmentDto(
    Guid Id,
    string Cif,
    string BorrowerName,
    string IndustrySector,
    DateOnly FinancialStatementDate,
    DateOnly AssessmentDate,
    decimal TotalQuantitativeScore,
    decimal TotalQualitativeScore,
    decimal TotalIcrrScore,
    IcrrGrade RatingGrade,
    bool IsBoardApproved,
    string? BoardResolutionReference,
    string? BoardMitigationStrategy,
    string EvaluatedByUserId
);
