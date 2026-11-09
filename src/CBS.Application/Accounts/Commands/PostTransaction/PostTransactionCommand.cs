using CBS.Application.Accounts.DTOs;

namespace CBS.Application.Accounts.Commands.PostTransaction;

public record PostTransactionCommand(
    PostTransactionRequest Request,
    string UserId,
    string BranchCode);
