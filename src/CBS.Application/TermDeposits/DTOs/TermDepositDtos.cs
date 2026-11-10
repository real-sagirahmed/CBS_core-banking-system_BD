using CBS.Domain.Enums;

namespace CBS.Application.TermDeposits.DTOs;

public sealed record OpenTermDepositRequest(
    string ContractId,
    string FdrAccountNumber,
    string OperativeAccountNumber,
    decimal PrincipalAmount,
    int TenorMonths,
    decimal AnnualInterestRate,
    DateOnly StartDate,
    TermDepositMaturityInstruction MaturityInstruction);

public sealed record TermDepositContractResponseDto(
    string ContractId,
    string FdrAccountNumber,
    string OperativeAccountNumber,
    decimal PrincipalAmount,
    int TenorMonths,
    decimal AnnualInterestRate,
    DateOnly StartDate,
    DateOnly MaturityDate,
    string MaturityInstruction,
    decimal AccruedInterestProvision,
    string Status,
    bool IsLienMarked,
    decimal? LienAmount,
    string? LienReference);

public sealed record PrematureEncashmentRequest(
    DateOnly EncashmentDate,
    decimal PenaltyMarkdownPercentage = 2.00m,
    bool HasTin = true);

public sealed record PrematureEncashmentResponseDto(
    string ContractId,
    int DaysElapsed,
    decimal AppliedInterestRate,
    decimal GrossInterest,
    decimal WithholdingTax,
    decimal NetInterest,
    decimal PrincipalPayout,
    decimal TotalPayoutAmount,
    decimal ProvisionReversalAmount,
    string? VoucherNumber);

public sealed record MaturityRolloverResponseDto(
    string ContractId,
    string MaturityInstruction,
    decimal RolledPrincipal,
    decimal NetInterestCapitalized,
    decimal WithholdingTaxDeducted,
    decimal PushedToOperativeAccount,
    DateOnly NewMaturityDate,
    decimal NewInterestRate,
    string? VoucherNumber);

public sealed record MarkLienRequest(
    decimal LoanAmount,
    string LoanReference);
