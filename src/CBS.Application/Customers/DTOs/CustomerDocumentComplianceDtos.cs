using CBS.Domain.Enums;

namespace CBS.Application.Customers.DTOs;

public record ScanExpiredDocumentsRequest(
    DateOnly AsOfDate,
    string EvaluatorUserId = "COMPLIANCE01");

public record ExpiredDocumentItemDto(
    Guid DocumentId,
    string CustomerCif,
    string Category,
    string DocumentNumber,
    DateOnly ExpiryDate,
    int DaysOverdue,
    bool RestraintApplied);

public record ExpiredDocumentScanSummaryDto(
    DateOnly AsOfDate,
    int TotalScanned,
    int ExpiredCount,
    int GracePeriodBreachedCount,
    int RestraintsAppliedCount,
    List<ExpiredDocumentItemDto> Details);

public record RenewDocumentRequest(
    string NewDocumentNumber,
    DateOnly NewIssueDate,
    DateOnly NewExpiryDate,
    string MakerUserId,
    string CheckerUserId);

public record RenewDocumentResponseDto(
    Guid DocumentId,
    string CustomerCif,
    string DocumentNumber,
    DateOnly ExpiryDate,
    bool IsRestraintReleased,
    string Message);

public record FatcaComplianceEvaluationRequest(
    string CustomerCif,
    bool HasUsCitizenship,
    bool HasUsGreenCard,
    bool BornInUs,
    string? UsAddress,
    string? UsPhone,
    bool HasStandingOrderToUsAccount,
    bool HasUsPowerOfAttorney,
    string? W8BenOrW9Reference,
    string? GiinNumber);

public record FatcaAssessmentDto(
    string CustomerCif,
    string Classification,
    bool IsReportableUnderFatca,
    List<string> IndiciaMatched,
    string? W8BenOrW9Reference,
    string? GiinNumber,
    DateTime EvaluatedAtUtc);
