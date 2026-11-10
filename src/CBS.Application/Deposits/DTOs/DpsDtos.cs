using CBS.Domain.Enums;

namespace CBS.Application.Deposits.DTOs;

public sealed record OpenDpsRequest(
    string DpsAccountNumber,
    string LinkedCasaAccountNumber,
    string CustomerCif,
    decimal MonthlyInstallment,
    string Currency,
    int TenorYears,
    decimal ContractedRate,
    decimal PrematurePenaltyRate,
    int DebitDayOfMonth,
    DateOnly StartDate,
    DpsSettlementMode SettlementMode = DpsSettlementMode.LumpSumCreditToCasa);

public sealed record DpsAccountResponseDto(
    string Id,
    string DpsAccountNumber,
    string LinkedCasaAccountNumber,
    string CustomerCif,
    decimal MonthlyInstallment,
    string Currency,
    int TenorYears,
    decimal ContractedRate,
    decimal PrematurePenaltyRate,
    int DebitDayOfMonth,
    DateOnly StartDate,
    DateOnly MaturityDate,
    decimal TotalPrincipalPaid,
    decimal TotalInterestAccrued,
    int PaidInstallments,
    int TotalInstallments,
    int ConsecutiveDefaults,
    string Status,
    bool IsLienMarked,
    string? ActiveLienFacilityId,
    IReadOnlyList<DpsInstallmentDto> Installments);

public sealed record DpsInstallmentDto(
    int InstallmentNumber,
    DateOnly DueDate,
    DateOnly? PaidDate,
    decimal DueAmount,
    decimal LateFee,
    string Status,
    string? VoucherNumber);

public sealed record CollectInstallmentRequest(
    int InstallmentNumber,
    decimal PaidAmount,
    DateOnly PaymentDate,
    decimal? LateFee = null,
    string? VoucherNumber = null);

public sealed record PrematureDpsQuoteResponse(
    string DpsAccountId,
    decimal TotalPrincipal,
    decimal GrossInterest,
    decimal WithholdingTax,
    decimal NetPayableAmount,
    decimal EffectiveRate,
    int MonthsCompleted,
    string Currency);

public sealed record PrematureEncashDpsRequest(
    DateOnly EncashmentDate,
    bool HasTaxIdNumber,
    string OfficerId,
    string? Remarks = null);

public sealed record ProcessDpsSweepsRequest(
    DateOnly EvaluationDate);
