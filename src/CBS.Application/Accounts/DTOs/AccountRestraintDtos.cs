using CBS.Domain.Accounts;
using CBS.Domain.Enums;

namespace CBS.Application.Accounts.DTOs;

public record PlaceRestraintRequest(
    string RestraintType,
    string Scope,
    decimal HoldAmount,
    string ReasonCategory,
    string NarrativeReason,
    string IssuingAuthority,
    string ReferenceCaseNo,
    string MakerUserId,
    string CheckerUserId,
    DateOnly? ExpiryDate = null);

public record ReleaseRestraintRequest(
    string MakerUserId,
    string CheckerUserId,
    string ReleaseRemarks);

public record RealizeRestraintRequest(
    string CheckerUserId,
    decimal RealizeAmount,
    string SettlementGl,
    string Remarks);

public record AccountRestraintResponseDto(
    string RestraintId,
    string AccountNumber,
    string RestraintType,
    string Scope,
    decimal HoldAmount,
    string ReasonCategory,
    string NarrativeReason,
    string IssuingAuthority,
    string ReferenceCaseNo,
    string PlacedByUserId,
    string AuthorizedByUserId,
    DateTime CreatedAtUtc,
    DateOnly? ExpiryDate,
    string Status,
    string? ReleasedByUserId,
    string? ReleaseAuthorizedByUserId,
    DateTime? ReleasedAtUtc,
    string? ReleaseRemarks,
    decimal? ForeclosedAmount,
    string? ForeclosedSettlementGl,
    DateTime? ForeclosedAtUtc);

public record AccountBalanceSummaryDto(
    string AccountNumber,
    string OperatingStatus,
    decimal LedgerBalance,
    decimal TotalActiveHoldAmount,
    decimal AvailableBalance,
    int ActiveRestraintsCount);
