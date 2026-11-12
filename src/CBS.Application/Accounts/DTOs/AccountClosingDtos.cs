namespace CBS.Application.Accounts.DTOs;

public record AccountClosingQuoteDto(
    string AccountNumber,
    decimal LedgerBalance,
    decimal ClosingFee,
    decimal VatAmount,
    decimal NetRefundAmount,
    bool CanClose,
    IReadOnlyList<string> ValidationErrors);

public record CloseAccountRequest(
    string MakerUserId,
    string CheckerUserId,
    string Reason,
    string SettlementOption,
    string? BeneficiaryAccountNumber = null);

public record AccountClosingResponseDto(
    string AccountNumber,
    string Status,
    string ClosedByUserId,
    string ClosedAuthorizedByUserId,
    DateOnly ClosedDate,
    decimal ClosingFeeDeducted,
    decimal VatDeducted,
    decimal NetSettlementAmount,
    string ClosingReason);
