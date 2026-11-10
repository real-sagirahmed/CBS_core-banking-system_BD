using CBS.Domain.Enums;

namespace CBS.Application.Remittance;

public record ProcessInwardRemittanceRequest(
    string? TransactionReference,
    ForeignRemittanceType RemittanceType,
    string SenderName,
    string SenderCountry,
    string? SenderBankBic,
    string BeneficiaryName,
    string BeneficiaryAccountNumber,
    string ForeignCurrency,
    decimal ForeignAmount,
    decimal ExchangeRate,
    RemittanceChargeParty ChargeParty,
    string? PurposeOfRemittance,
    SanctionStatus? SanctionStatus,
    string MakerUserId);

public record AuthorizeInwardRemittanceRequest(
    string CheckerUserId);

public record InitiateOutwardRemittanceRequest(
    string? TransactionReference,
    ForeignRemittanceType RemittanceType,
    string SenderName,
    string SenderCountry,
    string? SenderBankBic,
    string BeneficiaryName,
    string SenderDebitAccountNumber,
    string ForeignCurrency,
    decimal ForeignAmount,
    decimal ExchangeRate,
    RemittanceChargeParty ChargeParty,
    string? PurposeOfRemittance,
    string? QuotaReference,
    decimal? QuotaLimitAnnualBdt,
    decimal? QuotaUtilizedAnnualBdt,
    SanctionStatus? SanctionStatus,
    string MakerUserId);

public record AuthorizeOutwardRemittanceRequest(
    string CheckerUserId);

public record CancelRemittanceRequest(
    string Reason,
    string RequestedByUserId);

public record BatchIncentiveClaimRequest(
    string ClaimPeriod,
    string PreparedByUserId);

public record SubmitIncentiveClaimRequest(
    string CentralBankSubmissionReference);

public record ForeignRemittanceResponse(
    Guid Id,
    string TransactionReference,
    string Direction,
    string RemittanceType,
    string SenderName,
    string SenderCountry,
    string? SenderBankBic,
    string BeneficiaryName,
    string? BeneficiaryAccountNumber,
    string ForeignCurrency,
    decimal ForeignAmount,
    decimal ExchangeRate,
    decimal ConvertedBdtAmount,
    decimal IncentiveRate,
    decimal IncentiveAmount,
    decimal TotalDisbursedBdtAmount,
    string ChargeParty,
    string SanctionStatus,
    string Status,
    string IncentiveStatus,
    Guid? CentralBankClaimBatchId,
    string? PurposeOfRemittance,
    string? QuotaReference,
    decimal? QuotaLimitAnnualBdt,
    decimal? QuotaUtilizedAnnualBdt,
    string MakerUserId,
    string? CheckerUserId,
    DateTime CreatedAtUtc,
    DateTime? AuthorizedAtUtc,
    DateTime? DisbursedAtUtc,
    string? CancellationReason);

public record IncentiveClaimBatchResponse(
    Guid Id,
    string BatchNumber,
    string ClaimPeriod,
    int TotalTransactionCount,
    decimal TotalClaimedIncentiveBdt,
    string Status,
    string PreparedByUserId,
    DateTime BatchedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? ReimbursedAtUtc,
    string? CentralBankReference);
