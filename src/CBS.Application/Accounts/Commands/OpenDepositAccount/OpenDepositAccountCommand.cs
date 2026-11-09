using CBS.Application.Accounts.DTOs;

namespace CBS.Application.Accounts.Commands.OpenDepositAccount;

public record OpenDepositAccountCommand(
    OpenDepositAccountRequest Request,
    string MakerUserId,
    string BranchCode);
