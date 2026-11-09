using CBS.Application.Common.Interfaces;
using CBS.Application.PaymentRails.DTOs;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.PaymentRails;
using CBS.Domain.ValueObjects;

namespace CBS.Application.PaymentRails.Commands.ProcessInwardCheque;

public record ProcessInwardChequeClearingCommand(ProcessInwardChequeClearingRequest Request);

public class ProcessInwardChequeClearingCommandHandler
{
    private readonly IChequeClearingRepository _chequeRepository;
    private readonly IAccountRepository _accountRepository;

    public ProcessInwardChequeClearingCommandHandler(
        IChequeClearingRepository chequeRepository,
        IAccountRepository accountRepository)
    {
        _chequeRepository = chequeRepository;
        _accountRepository = accountRepository;
    }

    public async Task<InwardClearingDecisionResponseDto> HandleAsync(ProcessInwardChequeClearingCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var amount = Money.Bdt(req.Amount);
        var draweeAccountNum = AccountNumber.From(req.DraweeAccountNumber);
        var presentationDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var draweeAccount = await _accountRepository.GetByAccountNumberAsync(draweeAccountNum, ct);

        // Construct Inward Clearing Item
        var ourRouting = RoutingNumber.From("015270112"); // Our bank routing
        var presentingRouting = RoutingNumber.From(req.PresentingBankRouting);
        var micr = new MicrLine(req.ChequeNumber, ourRouting, draweeAccountNum, "10");

        var item = new ChequeClearingItem(
            Guid.NewGuid(),
            req.BatchId,
            ClearingDirection.Inward,
            micr,
            amount,
            presentingRouting,
            ourRouting,
            beneficiaryAccountNumber: "OUTSIDE_BENEFICIARY",
            req.ChequeIssueDate,
            presentationDate);

        decimal? remainingBalance = draweeAccount?.Balance.Amount;

        // If item was already auto-returned (e.g. stale or post-dated), persist and return decision immediately
        if (item.Status == ChequeClearingStatus.Returned)
        {
            await _chequeRepository.AddAsync(item, ct);
            return new InwardClearingDecisionResponseDto(
                item.Id,
                req.ChequeNumber,
                req.DraweeAccountNumber,
                req.Amount,
                item.Status,
                item.ReturnReason,
                item.ReturnRemarks,
                remainingBalance);
        }

        // Validate Drawee Account Rules (BRPD 1.5.3)
        if (draweeAccount is null)
        {
            item.ReturnCheque(BachReturnReason.AccountClosedOrDormant, "Drawee account does not exist or has been permanently closed.");
        }
        else if (draweeAccount.Status == AccountOperatingStatus.Dormant)
        {
            item.ReturnCheque(BachReturnReason.AccountClosedOrDormant, $"Drawee account '{draweeAccount.Id.Value}' is DORMANT under BRPD 3.1.20.");
        }
        else if (draweeAccount.Balance < amount)
        {
            item.ReturnCheque(BachReturnReason.InsufficientFunds, $"Insufficient funds in drawer account. Current balance: {draweeAccount.Balance}, Required: {amount}.");
        }
        else
        {
            // Valid presentment: Debit drawee account and settle
            draweeAccount.Debit(amount, $"BACH Inward Cheque #{req.ChequeNumber}");
            await _accountRepository.UpdateAsync(draweeAccount, ct);
            remainingBalance = draweeAccount.Balance.Amount;

            item.Settle(presentationDate);
        }

        await _chequeRepository.AddAsync(item, ct);

        return new InwardClearingDecisionResponseDto(
            item.Id,
            req.ChequeNumber,
            req.DraweeAccountNumber,
            req.Amount,
            item.Status,
            item.ReturnReason,
            item.ReturnRemarks,
            remainingBalance);
    }
}
