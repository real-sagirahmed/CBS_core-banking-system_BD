using CBS.Application.Common.Interfaces;
using CBS.Application.PaymentRails.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.PaymentRails;
using CBS.Domain.ValueObjects;

namespace CBS.Application.PaymentRails.Commands.InitiateBeftn;

public record InitiateBeftnTransferCommand(InitiateBeftnTransferRequest Request);

public class InitiateBeftnTransferCommandHandler
{
    private readonly IBeftnRepository _beftnRepository;
    private readonly IAccountRepository _accountRepository;

    public InitiateBeftnTransferCommandHandler(
        IBeftnRepository beftnRepository,
        IAccountRepository accountRepository)
    {
        _beftnRepository = beftnRepository;
        _accountRepository = accountRepository;
    }

    public async Task<BeftnBatchItemResponseDto> HandleAsync(InitiateBeftnTransferCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var amount = Money.Bdt(req.Amount);
        var origAccNum = AccountNumber.From(req.OriginatingAccount);

        var origAccount = await _accountRepository.GetByAccountNumberAsync(origAccNum, ct)
            ?? throw new KeyNotFoundException($"Originating deposit account '{req.OriginatingAccount}' was not found.");

        // Debit originating customer account for outgoing fund transfer
        origAccount.Debit(amount, $"BEFTN Transfer to {req.BeneficiaryName} ({req.DestinationAccount})");
        await _accountRepository.UpdateAsync(origAccount, ct);

        var origRouting = RoutingNumber.From(req.OriginatingRouting);
        var destRouting = RoutingNumber.From(req.DestinationRouting);
        var subDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var item = new BeftnBatchItem(
            Guid.NewGuid(),
            req.BatchId,
            ClearingDirection.Outward,
            req.Type,
            amount,
            origRouting,
            req.OriginatingAccount,
            destRouting,
            req.DestinationAccount,
            req.BeneficiaryName,
            req.PaymentReference,
            subDate);

        item.SendToBangladeshBank();
        await _beftnRepository.AddAsync(item, ct);

        return new BeftnBatchItemResponseDto(
            item.Id,
            item.BatchId,
            item.Direction,
            item.Type,
            item.Amount.Amount,
            item.OriginatingRouting.Value,
            item.OriginatingAccount,
            item.DestinationRouting.Value,
            item.DestinationAccount,
            item.BeneficiaryName,
            item.PaymentReference,
            item.Status,
            item.SubmissionDate,
            item.SettlementDate);
    }
}
