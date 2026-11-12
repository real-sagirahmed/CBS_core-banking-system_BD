// ARCHITECTURAL INTENT: DTOs and commands for Loan Asset Classification, Qualitative Judgment,
// Rescheduling lifecycle, and Statutory Write-Off & Recovery.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 & 4.2.

using CBS.Domain.Enums;

namespace CBS.Application.Lending.DTOs;

public sealed record ApplyQualitativeJudgmentCommand(
    string LoanAccountNumber,
    LoanClassificationGrade Grade,
    QualitativeJudgmentReason Reason,
    string JustificationNotes,
    string? InspectionReportReference,
    string InspectorUserId,
    string CheckerUserId
);

public sealed record RevokeQualitativeJudgmentCommand(
    string LoanAccountNumber,
    string InspectorUserId,
    string CheckerUserId,
    string RevocationReason
);

public sealed record RescheduleLoanCommand(
    string LoanAccountNumber,
    int RescheduleIteration,
    decimal DownPaymentPaid,
    decimal RequiredDownPayment,
    DateOnly NewMaturityDate,
    decimal NewInterestRate,
    RescheduleApprovalAuthority ApprovalAuthority,
    string? BoardResolutionReference,
    DateOnly? BoardApprovalDate,
    decimal? InterestWaiver,
    string RestructuringNotes,
    string MakerUserId,
    string CheckerUserId
);

public sealed record WriteOffLoanCommand(
    string LoanAccountNumber,
    decimal SpecificProvisionAvailable,
    string BoardResolutionNumber,
    DateOnly BoardApprovalDate,
    LegalCaseType LegalCaseType,
    string? ArthaRinCaseNumber,
    DateOnly? ArthaRinFilingDate,
    string MakerUserId,
    string CheckerUserId,
    string JustificationNotes
);

public sealed record RecordWriteOffRecoveryCommand(
    string LoanAccountNumber,
    decimal Amount,
    string RecoverySource,
    string ReferenceNumber,
    string ReceivedByUserId
);

public sealed record QualitativeJudgmentResultDto(
    Guid RecordId,
    string LoanAccountNumber,
    LoanClassificationGrade OriginalGrade,
    LoanClassificationGrade AppliedGrade,
    QualitativeJudgmentReason Reason,
    string JustificationRemarks,
    bool IsActive,
    string InspectorUserId,
    string CheckerUserId,
    DateOnly EffectiveDate,
    DateTime? RevokedAtUtc,
    string? RevocationRemarks
);

public sealed record RescheduleResultDto(
    Guid RescheduleId,
    string LoanAccountNumber,
    int RescheduleIteration,
    decimal DownPaymentPaid,
    decimal RequiredDownPayment,
    decimal RescheduledPrincipal,
    decimal InterestSuspenseBalance,
    DateOnly NewMaturityDate,
    decimal NewInterestRate,
    RescheduleApprovalAuthority ApprovalAuthority,
    string? BoardResolutionReference,
    string Status
);

public sealed record WriteOffResultDto(
    Guid WriteOffId,
    string LoanAccountNumber,
    decimal WrittenOffPrincipal,
    decimal WrittenOffInterestSuspense,
    decimal TotalWrittenOffAmount,
    decimal MaintainedSpecificProvision,
    string BoardResolutionNumber,
    string? ArthaRinCaseNumber,
    LegalCaseType LegalCaseType,
    DateTime WrittenOffAtUtc,
    string Status
);

public sealed record WriteOffRecoveryResultDto(
    Guid WriteOffId,
    string LoanAccountNumber,
    decimal AmountRecovered,
    decimal AllocatedToPrincipal,
    decimal AllocatedToInterest,
    decimal CumulativeRecoveredAmount,
    string Status,
    DateTime RecoveredAtUtc
);

public sealed record LoanClassificationEvaluationItemDto(
    string LoanAccountNumber,
    string LoanType,
    decimal OutstandingBalance,
    int Dpd,
    LoanClassificationGrade PreviousGrade,
    LoanClassificationGrade NewGrade,
    bool HasQualitativeJudgment,
    decimal EligibleCollateralFsv,
    decimal BaseForProvision,
    decimal RequiredGeneralProvision,
    decimal RequiredSpecificProvision,
    decimal InterestSuspenseBalance
);

public sealed record LoanClassificationBatchResultDto(
    string BranchCode,
    DateTime EvaluationDate,
    int TotalAccountsEvaluated,
    int UpgradesCount,
    int DowngradesCount,
    int UnchangedCount,
    decimal TotalPortfolioOutstanding,
    decimal TotalGeneralProvisionRequired,
    decimal TotalSpecificProvisionRequired,
    IReadOnlyList<LoanClassificationEvaluationItemDto> EvaluatedAccounts
);

public sealed record LoanClassificationSummaryDto(
    LoanClassificationGrade Grade,
    int AccountCount,
    decimal TotalOutstanding,
    decimal TotalGeneralProvision,
    decimal TotalSpecificProvision
);

public sealed record LoanPortfolioClassificationReportDto(
    DateTime ReportGeneratedAtUtc,
    string? BranchCode,
    int TotalAccounts,
    decimal TotalOutstanding,
    decimal TotalEligibleCollateralFsv,
    decimal TotalBaseForProvision,
    decimal TotalGeneralProvision,
    decimal TotalSpecificProvision,
    IReadOnlyList<LoanClassificationSummaryDto> GradeSummaries,
    IReadOnlyList<LoanClassificationEvaluationItemDto> Accounts
);

public sealed record WrittenOffRegisterItemDto(
    Guid WriteOffId,
    string LoanAccountNumber,
    string CustomerCif,
    decimal WrittenOffPrincipal,
    decimal WrittenOffInterestSuspense,
    decimal CumulativeRecoveredAmount,
    string BoardResolutionNumber,
    string? ArthaRinCaseNumber,
    DateOnly? ArthaRinFilingDate,
    DateOnly WriteOffDate,
    string Status
);
