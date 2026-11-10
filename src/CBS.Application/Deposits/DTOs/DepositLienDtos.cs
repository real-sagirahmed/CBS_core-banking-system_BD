namespace CBS.Application.Deposits.DTOs;

public sealed record PlaceDepositLienRequest(
    string DepositAccountId,
    string DepositType,
    string LoanAccountId,
    decimal SanctionedLoanLimit,
    decimal EncumberedAmount,
    string Currency,
    decimal MaximumLtvPercentage,
    string OfficerId,
    string ReferenceDocument);

public sealed record DepositLienResponseDto(
    string LienId,
    string DepositAccountId,
    string DepositType,
    string LoanAccountId,
    decimal SanctionedLoanLimit,
    decimal EncumberedAmount,
    string Currency,
    decimal MaximumLtvPercentage,
    string Status,
    DateTime PlacedAtUtc,
    DateTime? ReleasedAtUtc,
    string PlacedByOfficerId,
    string? ReleasedByOfficerId,
    string ReferenceDocument);

public sealed record ReleaseDepositLienRequest(
    string OfficerId,
    string Reason);
