using CBS.Application.Accounts.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Accounts.Commands.PostTransaction;

public class PostTransactionCommandHandler
{
    private readonly IAccountRepository _accountRepository;

    public PostTransactionCommandHandler(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
    }

    public async Task<TransactionResponseDto> HandleAsync(PostTransactionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        var req = command.Request;

        if (req.Amount <= 0m) throw new ArgumentException("Transaction amount must be positive.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.UserId)) throw new ArgumentException("User ID is required.", nameof(command));

        var accNum = AccountNumber.From(req.AccountNumber);
        var account = await _accountRepository.GetByAccountNumberAsync(accNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Account '{req.AccountNumber}' not found.");

        decimal previousBalance = account.Balance.Amount;
        decimal recoveredHovering = 0m;

        if (req.Type == DebitCredit.Credit)
        {
            var recovered = account.Credit(Money.Bdt(req.Amount), req.Narration, req.IsCustomerInduced);
            recoveredHovering = recovered.Amount;
        }
        else
        {
            account.Debit(Money.Bdt(req.Amount), req.Narration, req.IsCustomerInduced);
        }

        await _accountRepository.UpdateAsync(account, cancellationToken);

        return new TransactionResponseDto(
            account.Id.Value,
            previousBalance,
            account.Balance.Amount,
            req.Amount,
            req.Type.ToString(),
            recoveredHovering);
    }
}
