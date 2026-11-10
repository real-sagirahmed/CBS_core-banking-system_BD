using CBS.Domain.Enums;

namespace CBS.Application.GeneralLedger.DTOs;

public record VoucherEntryDto(
    Guid Id,
    string AccountCode,
    DebitCredit Type,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    decimal BaseAmount,
    string Narration);

public record FinancialVoucherResponseDto(
    Guid Id,
    string VoucherNumber,
    string BranchSol,
    VoucherType Type,
    DateOnly ValueDate,
    DateTime CreatedAtUtc,
    VoucherStatus Status,
    string MakerUserId,
    string? CheckerUserId,
    Guid? ReversalOfVoucherId,
    List<VoucherEntryDto> Entries);

public record ChartOfAccountDto(
    string Code,
    string Name,
    AccountCategory Category,
    string? ParentCode,
    int Level,
    bool IsLeaf,
    bool IsActive,
    string CurrencyCode);

public record TrialBalanceLineDto(
    string AccountCode,
    string AccountName,
    AccountCategory Category,
    decimal DebitSum,
    decimal CreditSum,
    decimal NetBalance,
    string NormalBalance);

public record TrialBalanceDto(
    DateOnly AsOfDate,
    string? BranchSol,
    List<TrialBalanceLineDto> Lines,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal Discrepancy,
    bool IsBalanced);

public record ReverseVoucherRequest(
    string Reason,
    string ReversalMakerUserId,
    string ReversalCheckerUserId);
