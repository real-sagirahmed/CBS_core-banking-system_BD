using CBS.Domain.Enums;

namespace CBS.Application.Adc.DTOs;

public sealed record ProcessIso8583Request(
    string Mti,
    string CardNumber,
    string ProcessingCode,
    decimal Amount,
    string Stan,
    string Rrn,
    string TerminalId,
    string AccountNumber,
    AdcChannelType Channel = AdcChannelType.Atm);

public sealed record ProcessIso8583Response(
    string Mti,
    string Stan,
    string Rrn,
    string ResponseCode,
    string StatusMessage,
    decimal? ApprovedAmount,
    decimal? RemainingBalance,
    string? VoucherNumber,
    decimal? TerminalRemainingCash);

public sealed record AtmTerminalDto(
    string TerminalId,
    string TerminalName,
    string BranchCode,
    string Location,
    decimal CurrentCashBalance,
    decimal MaxCapacity,
    bool IsActive,
    DateTime LastReplenishedAtUtc);

public sealed record SeedAtmTerminalRequest(
    string TerminalId,
    string TerminalName,
    string BranchCode,
    string Location,
    decimal InitialCash,
    decimal MaxCapacity);

public sealed record CallCenterAccountSummaryDto(
    string AccountNumber,
    string AccountType,
    string Currency,
    decimal AvailableBalance,
    string Status);

public sealed record CallCenterCustomerViewDto(
    string CifNumber,
    string FullName,
    string CustomerType,
    string RiskRating,
    IReadOnlyList<CallCenterAccountSummaryDto> Accounts);
