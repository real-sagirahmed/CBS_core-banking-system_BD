using CBS.Domain.Enums;

namespace CBS.Application.Accounts.DTOs;

public record OpenDepositAccountRequest(
    string CifNumber,
    DepositProductType ProductType,
    string BranchCode,
    decimal InitialDeposit = 0,
    bool HasTin = false);

public record AccountResponseDto(
    string AccountNumber,
    string CifNumber,
    string ProductType,
    string Status,
    decimal Balance,
    string Currency,
    bool HasTin,
    string BranchCode);

public record PostTransactionRequest(
    string AccountNumber,
    DebitCredit Type,
    decimal Amount,
    string Narration,
    bool IsCustomerInduced = true);

public record TransactionResponseDto(
    string AccountNumber,
    decimal PreviousBalance,
    decimal NewBalance,
    decimal Amount,
    string Type,
    decimal RecoveredHovering = 0);

public record EodBatchRequest(string BranchCode);

public record EodBatchResponseDto(
    string BranchCode,
    string Status,
    string BusinessDate,
    int PendingVouchers,
    string Message);
