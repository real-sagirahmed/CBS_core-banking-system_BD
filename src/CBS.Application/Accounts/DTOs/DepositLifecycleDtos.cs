using CBS.Domain.Accounts;

namespace CBS.Application.Accounts.DTOs;

public record DepositEodSummaryDto(
    string BranchCode,
    string BusinessDate,
    int TotalAccountsProcessed,
    int DormancyAppliedCount,
    int InoperativeAppliedCount,
    int UnclaimedNoticesSentCount,
    int UnclaimedTransferredCount,
    decimal TotalTransferredAmount,
    decimal TotalInterestAccrued,
    string? AccrualVoucherNumber,
    string Message);

public record InterestCapitalizationResponseDto(
    string AccountNumber,
    decimal GrossInterest,
    decimal WithholdingTax,
    decimal NetInterestCredited,
    decimal RecoveredHoveringCharges,
    decimal NewBalance,
    string? VoucherNumber);

public record BatchInterestCapitalizationResponseDto(
    string BranchCode,
    int AccountsCapitalizedCount,
    decimal TotalGrossInterest,
    decimal TotalTaxDeducted,
    decimal TotalNetCredited,
    IReadOnlyList<InterestCapitalizationResponseDto> Details);

public record UnclaimedAccountAgeingDto(
    string AccountNumber,
    string Cif,
    string ProductType,
    decimal Balance,
    string LastTransactionDate,
    int InactiveMonths,
    string AgeingCategory,
    int NoticesServedCount,
    string Status);

public record RequestDormancyReactivationRequest(
    string KycDocumentReference,
    string Remarks);

public record ApproveDormancyReactivationRequest(
    string Remarks);

public record DepositLifecycleEodRequest(
    string BranchCode,
    string? BusinessDate);

public record DepositInterestCapitalizationRequest(
    string BranchCode,
    string? BusinessDate);

public record DormancyReactivationResponseDto(
    string AccountNumber,
    string Status,
    string RequestedBy,
    string ApprovedBy,
    DateTime ApprovedAtUtc,
    string Message);
