using CBS.Domain.Enums;

namespace CBS.Application.SecurityAudit.DTOs;

public sealed record LoginSessionRequest(
    string SessionId,
    string UserId,
    UserRole Role,
    string BranchCode,
    string TerminalIp);

public sealed record LogoutSessionRequest(
    string SessionId);

public sealed record UserSessionResponse(
    string SessionId,
    string UserId,
    string Role,
    string BranchCode,
    string TerminalIp,
    DateTime LoggedInAtUtc,
    bool IsActive);

public sealed record AssertFinancialPermissionRequest(
    string SessionId);

public sealed record LogAuditEntryRequest(
    string UserId,
    UserRole Role,
    string BranchCode,
    AuditEventCategory EventCategory,
    string Action,
    string TargetEntityName,
    string TargetEntityId,
    string? BeforeStateJson,
    string? AfterStateJson,
    string TerminalIp);

public sealed record AuditJournalResponse(
    long SequenceNumber,
    DateTime TimestampUtc,
    string UserId,
    string Role,
    string BranchCode,
    string EventCategory,
    string Action,
    string TargetEntityName,
    string TargetEntityId,
    string PreviousEntryHash,
    string CurrentEntryHash,
    bool IsHashValid);

public sealed record CaatsAuditExtractResponse(
    int TotalRecords,
    bool IsChainIntegrityValid,
    IReadOnlyList<AuditJournalResponse> Entries);

public sealed record CustomerCashAggregateInput(
    string CifNumber,
    string CustomerName,
    int TransactionCount,
    decimal TotalCashAmount);

public sealed record GenerateCtrReportRequest(
    string ReportReference,
    DateTime ReportDate,
    IReadOnlyList<CustomerCashAggregateInput> Aggregates,
    string GeneratedByUserId);

public sealed record StrAlertInput(
    string CifNumber,
    string CustomerName,
    StrAlertReason Reason,
    decimal TransactionAmount,
    string Currency,
    string Narrative);

public sealed record EvaluateStrAlertsRequest(
    string ReportReference,
    DateTime ReportDate,
    IReadOnlyList<StrAlertInput> Alerts,
    string GeneratedByUserId);

public sealed record CtrItemResponse(
    string CifNumber,
    string CustomerName,
    int TransactionCount,
    decimal TotalCashAmount,
    string Currency,
    DateTime ReportDate);

public sealed record StrItemResponse(
    string CifNumber,
    string CustomerName,
    string Reason,
    decimal TransactionAmount,
    string Currency,
    string Narrative,
    DateTime DetectedAtUtc);

public sealed record BfiuReportResponse(
    string ReportReference,
    string ReportType,
    DateTime ReportDate,
    string Status,
    int TotalItems,
    IReadOnlyList<CtrItemResponse>? CtrItems,
    IReadOnlyList<StrItemResponse>? StrAlerts);
