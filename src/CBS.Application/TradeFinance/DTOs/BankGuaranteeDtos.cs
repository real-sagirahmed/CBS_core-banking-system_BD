using CBS.Domain.Enums;

namespace CBS.Application.TradeFinance.DTOs;

public record IssueBankGuaranteeRequest(
    string BgNumber,
    GuaranteeType Type,
    string ApplicantCif,
    string BeneficiaryName,
    string BeneficiaryAddress,
    string IssuingBranchCode,
    decimal GuaranteeAmount,
    decimal MarginPercentage,
    string? CollateralDepositAccount,
    decimal CommissionRatePerQuarter,
    DateOnly IssueDate,
    DateOnly ExpiryDate,
    int ClaimPeriodDays,
    GuaranteeExpiryType ExpiryType,
    string MakerUserId,
    string CheckerUserId,
    string MisSectorCode = "SEC-TRADE-BG");

public record AmendBankGuaranteeRequest(
    string BgNumber,
    decimal? AdditionalAmount,
    DateOnly? ExtendedExpiryDate,
    string ApprovalReference,
    string CheckerUserId);

public record InvokeGuaranteeClaimRequest(
    string BgNumber,
    string ClaimLetterReference,
    DateOnly ClaimDate,
    string SupervisorUserId);

public record LiquidateGuaranteeClaimRequest(
    string BgNumber,
    string SupervisorUserId);

public record CancelBankGuaranteeRequest(
    string BgNumber,
    string ReleaseLetterReference,
    bool OriginalReturned,
    DateOnly CancelDate,
    string SupervisorUserId);

public record BankGuaranteeResponse(
    Guid Id,
    string BgNumber,
    string Type,
    string ApplicantCif,
    string BeneficiaryName,
    string BeneficiaryAddress,
    string IssuingBranchCode,
    decimal GuaranteeAmount,
    decimal CashMarginAmount,
    string? CollateralDepositAccountNumber,
    decimal MarginPercentage,
    decimal CommissionRatePerQuarter,
    decimal CommissionAmount,
    decimal VatAmount,
    decimal TotalFeePayable,
    DateOnly IssueDate,
    DateOnly ExpiryDate,
    int ClaimPeriodDays,
    string ExpiryType,
    string Status,
    string MakerUserId,
    string CheckerUserId,
    string MisSectorCode,
    string? AmendmentReference,
    string? ClaimLetterReference,
    DateOnly? ClaimDate,
    string? ReleaseLetterReference,
    bool OriginalReturned,
    DateOnly? CancelledDate);
