using System;
using CBS.Domain.Enums;

namespace CBS.Application.Security.DTOs;

// --- 1. Session Control DTOs ---

public sealed record LoginWithSessionControlRequest(
    string UserId,
    string RoleName,
    string TerminalIp,
    string AppServerIp,
    string BranchCode,
    ConcurrentSessionPolicy Policy = ConcurrentSessionPolicy.BlockNewSession);

public sealed record UserActiveSessionResponseDto(
    string SessionId,
    string UserId,
    string TerminalIp,
    string AppServerIp,
    string BranchCode,
    DateTime LoginTimeUtc,
    DateTime LastActivityUtc,
    bool IsActive,
    string? TerminationReason,
    string? TerminationNotes,
    DateTime? TerminatedAtUtc);

public sealed record TerminateSessionRequest(
    string TerminatedByUserId,
    SessionTerminationReason Reason,
    string? Notes = null);

// --- 2. Admin Dual Control DTOs ---

public sealed record CreateAdminChangeRequestDto(
    AdminChangeCategory Category,
    string ChangeDescription,
    string ChangePayloadJson,
    string MakerUserId,
    string MakerBranchCode);

public sealed record ApproveAdminChangeRequestDto(
    string CheckerUserId,
    string? Remarks = null);

public sealed record RejectAdminChangeRequestDto(
    string CheckerUserId,
    string RejectionReason);

public sealed record AdminChangeRequestResponseDto(
    string RequestId,
    string Category,
    string ChangeDescription,
    string ChangePayloadJson,
    string MakerUserId,
    string MakerBranchCode,
    string Status,
    DateTime CreatedAtUtc,
    string? CheckerUserId,
    string? CheckerRemarks,
    DateTime? ActionedAtUtc,
    string? RejectionReason);

// --- 3. Bank Global Parameters DTOs ---

public sealed record BankGlobalParametersResponseDto(
    string BankCode,
    string BankName,
    string BaseCurrencyCode,
    string LocalCurrencyCode,
    string ChiefLocalOfficeBranchCode,
    string WeeklyHolidaysCsv,
    int FiscalYearStartMonth,
    int FiscalYearStartDay,
    bool RealTimeBranchSettlementEnabled,
    bool AllowAlphaCustomerNumber,
    int MaxExternalAccountNumberLength,
    string? DailySpotRateListDetailsJson,
    DateTime LastUpdatedAtUtc,
    string LastUpdatedByUserId);

public sealed record UpdateBankGlobalParametersRequest(
    string BankName,
    string BaseCurrencyCode,
    string LocalCurrencyCode,
    string ChiefLocalOfficeBranchCode,
    string WeeklyHolidaysCsv,
    int FiscalYearStartMonth,
    int FiscalYearStartDay,
    bool RealTimeBranchSettlementEnabled,
    bool AllowAlphaCustomerNumber,
    int MaxExternalAccountNumberLength,
    string? DailySpotRateListDetailsJson,
    string UpdatedByUserId);
