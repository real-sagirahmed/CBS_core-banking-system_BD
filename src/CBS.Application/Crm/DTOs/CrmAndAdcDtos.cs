using System;
using System.Collections.Generic;
using CBS.Domain.Enums;

namespace CBS.Application.Crm.DTOs;

// --- 1. Corporate Group & UBO DTOs ---

public sealed record AddGroupMemberDetailRequest(
    string CifNumber,
    string EntityName,
    CustomerGroupRole Role,
    decimal ShareholdingPercentage,
    string RegistrationNumber);

public sealed record RegisterBeneficialOwnerRequest(
    string FullName,
    string NationalId,
    BeneficialOwnerType OwnerType,
    decimal ShareholdingPercentage,
    bool IsPepOrIp,
    string? VerificationStatus);

public sealed record ValidateGroupExposureRequest(
    decimal ProposedNewFacilityBdt);

public sealed record GroupMemberDetailDto(
    string CifNumber,
    string EntityName,
    string Role,
    decimal ShareholdingPercentage,
    string RegistrationNumber,
    DateTime LinkedAtUtc);

public sealed record BeneficialOwnerDto(
    Guid OwnerId,
    string FullName,
    string NationalId,
    string OwnerType,
    decimal ShareholdingPercentage,
    bool IsPepOrIp,
    string VerificationStatus,
    DateTime RegisteredAtUtc);

public sealed record CorporateGroupDetailResponse(
    string GroupId,
    string GroupName,
    string ParentCifNumber,
    decimal StatutoryGroupLimitBdt,
    IReadOnlyList<GroupMemberDetailDto> MemberDetails,
    IReadOnlyList<BeneficialOwnerDto> BeneficialOwners,
    decimal TotalFundedExposureBdt,
    decimal TotalNonFundedExposureBdt,
    decimal TotalExposureBdt,
    decimal RemainingLimitBdt,
    bool IsBreached);


// --- 2. Customer Transaction Profile (TP) DTOs ---

public sealed record CreateTransactionProfileRequest(
    string CifNumber,
    decimal MonthlyEstimatedTurnover,
    decimal MaxCashDepositAmount,
    int MaxCashDepositCount,
    decimal MaxCashWithdrawalAmount,
    int MaxCashWithdrawalCount,
    decimal MaxTransferAmount,
    int MaxTransferCount,
    decimal MaxAdcAmount,
    int MaxAdcCount);

public sealed record RecordTpTransactionRequest(
    TpTransactionCategory Category,
    decimal Amount,
    string? Currency,
    DateTime? TransactionDate);

public sealed record ClearTpBreachRequest(
    string Justification,
    string ReviewedByUserId);

public sealed record CustomerTransactionProfileResponse(
    string CifNumber,
    decimal MonthlyEstimatedTurnover,
    decimal MaxMonthlyCashDepositAmount,
    int MaxMonthlyCashDepositCount,
    decimal MaxMonthlyCashWithdrawalAmount,
    int MaxMonthlyCashWithdrawalCount,
    decimal MaxMonthlyTransferAmount,
    int MaxMonthlyTransferCount,
    decimal MaxMonthlyAdcAmount,
    int MaxMonthlyAdcCount,
    string CurrentMonthYear,
    decimal CurrentMonthCashDepositAmount,
    int CurrentMonthCashDepositCount,
    decimal CurrentMonthCashWithdrawalAmount,
    int CurrentMonthCashWithdrawalCount,
    decimal CurrentMonthTransferAmount,
    int CurrentMonthTransferCount,
    decimal CurrentMonthAdcAmount,
    int CurrentMonthAdcCount,
    bool IsTpBreached,
    decimal MaxVariancePercentage,
    string? LastBreachReason,
    DateTime LastAssessedAtUtc,
    string? LastReviewedByUserId,
    DateTime? LastReviewedAtUtc);


// --- 3. ADC Central Reconciliation DTOs ---

public sealed record CreateReconciliationBatchRequest(
    string BatchId,
    AdcSettlementNetwork Network,
    DateOnly ReconciliationDate,
    string SettlementGlAccount,
    string SuspenseGlAccount,
    string CreatedByUserId);

public sealed record IngestSwitchRecordRequest(
    string Stan,
    string Rrn,
    string TerminalId,
    string AccountNumber,
    decimal SwitchAmount,
    decimal? CbsAmount,
    DateTime TransactionTime);

public sealed record GenerateRectificationVoucherRequest(
    Guid EntryId,
    RectificationAction Action,
    string CustomerOrTargetAccount,
    string AuthorizedByUserId,
    string Narration);

public sealed record CloseReconciliationBatchRequest(
    string ClosedByUserId);

public sealed record AdcReconciliationEntryDto(
    Guid EntryId,
    string Stan,
    string Rrn,
    string TerminalId,
    string AccountNumber,
    decimal SwitchAmount,
    decimal? CbsAmount,
    decimal DifferenceAmount,
    string MatchStatus,
    string? ActionTaken,
    string? RectificationVoucherNumber,
    bool IsRectified,
    DateTime TransactionTimestampUtc);

public sealed record RectificationVoucherDto(
    string VoucherNumber,
    Guid EntryId,
    string Stan,
    string Rrn,
    string DebitAccountOrGl,
    string CreditAccountOrGl,
    decimal VoucherAmount,
    string Action,
    string Narration,
    string AuthorizedByUserId,
    DateTime IssuedAtUtc);

public sealed record AdcReconciliationBatchResponse(
    string BatchId,
    string Network,
    DateOnly ReconciliationDate,
    string SettlementGlAccount,
    string SuspenseGlAccount,
    int TotalRecords,
    int TotalMatched,
    int TotalMismatched,
    decimal TotalDiscrepancyAmount,
    string Status,
    string CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    IReadOnlyList<AdcReconciliationEntryDto> Entries,
    IReadOnlyList<RectificationVoucherDto> Vouchers);
