using CBS.Application.Accounts.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Accounts.Commands.PostTransaction;

public class PostTransactionCommandHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public PostTransactionCommandHandler(
        IAccountRepository accountRepository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _glService = glService ?? throw new ArgumentNullException(nameof(glService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TransactionResponseDto> HandleAsync(PostTransactionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        var req = command.Request;

        if (req.Amount <= 0m) throw new ArgumentException("Transaction amount must be positive.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.UserId)) throw new ArgumentException("User ID is required.", nameof(command));

        var accNum = AccountNumber.From(req.AccountNumber);
        var account = req.Type == DebitCredit.Debit
            ? await _accountRepository.GetByAccountNumberForUpdateAsync(accNum, cancellationToken)
            : await _accountRepository.GetByAccountNumberAsync(accNum, cancellationToken);

        if (account is null) throw new KeyNotFoundException($"Account '{req.AccountNumber}' not found.");

        decimal previousBalance = account.Balance.Amount;
        decimal recoveredHovering = 0m;

        if (req.Type == DebitCredit.Credit)
        {
            var recovered = account.Credit(Money.Bdt(req.Amount), req.Narration, req.IsCustomerInduced);
            recoveredHovering = recovered.Amount;
            await _glService.PostCashDepositAsync(
                account.BranchCode,
                account.Id.Value,
                req.Amount,
                command.UserId,
                null,
                cancellationToken);
        }
        else
        {
            account.Debit(Money.Bdt(req.Amount), req.Narration, req.IsCustomerInduced);
            await _glService.PostCashWithdrawalAsync(
                account.BranchCode,
                account.Id.Value,
                req.Amount,
                command.UserId,
                null,
                cancellationToken);
        }

        await _accountRepository.UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TransactionResponseDto(
            account.Id.Value,
            previousBalance,
            account.Balance.Amount,
            req.Amount,
            req.Type.ToString(),
            recoveredHovering);
    }
}
