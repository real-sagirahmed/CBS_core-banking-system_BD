using CBS.Domain.ValueObjects;

namespace CBS.Application.BranchCash.DTOs;

public record IssueSdrRequest(
    string SdrNumber,
    string BranchCode,
    decimal Amount,
    string TenderOrRfpNumber,
    string BeneficiaryAuthority,
    string? PurchaserCif,
    WalkInCustomerDetails? WalkInDetails,
    DateOnly IssueDate,
    DateOnly ExpiryDate,
    string MakerUserId,
    string CheckerUserId);

public record LienSdrRequest(
    string SdrNumber);

public record ReleaseSdrRequest(
    string SdrNumber,
    string ReleaseLetterReference,
    DateOnly ReleaseDate,
    string SupervisorUserId);

public record ForfeitSdrRequest(
    string SdrNumber,
    string ForfeitureClaimReference,
    DateOnly ClaimDate,
    string SupervisorUserId);

public record StopSdrRequest(
    string SdrNumber,
    string Reason,
    string SupervisorUserId);

public record RevokeStopSdrRequest(
    string SdrNumber,
    string Reason,
    string SupervisorUserId);

public record IssueDuplicateSdrRequest(
    string OriginalSdrNumber,
    string NewSdrNumber,
    string IndemnityBondReference,
    string SupervisorUserId);

public record SdrResponse(
    Guid Id,
    string SdrNumber,
    string BranchCode,
    string? PurchaserCif,
    WalkInCustomerDetails? WalkInDetails,
    string TenderOrRfpNumber,
    string BeneficiaryAuthority,
    decimal Amount,
    decimal Commission,
    decimal VatAmount,
    decimal TotalPayable,
    DateOnly IssueDate,
    DateOnly ExpiryDate,
    string Status,
    string MakerUserId,
    string CheckerUserId,
    string? ReleaseLetterReference,
    string? ForfeitureClaimReference,
    DateOnly? SettledDate,
    string? StopReason,
    string? StopRevocationReason,
    string? DuplicateSdrNumber,
    string? IndemnityBondReference);

public record RevokeInstrumentStopPaymentRequest(
    string InstrumentNumber,
    string Reason,
    string SupervisorUserId);

public record StopPaymentJournalResponse(
    Guid Id,
    string InstrumentType,
    string InstrumentNumber,
    string BranchCode,
    string ActionType,
    string Reason,
    string AuthorizedByUserId,
    DateTime ActionDateUtc,
    string? IndemnityBondReference);
