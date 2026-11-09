using CBS.Application.Common.Interfaces;
using CBS.Application.PaymentRails.DTOs;
using CBS.Domain.PaymentRails;
using CBS.Domain.ValueObjects;

namespace CBS.Application.PaymentRails.Commands.Rtgs;

public record InitiateRtgsTransferCommand(InitiateRtgsTransferRequest Request);
public record SettleRtgsCommand(SettleRtgsRequest Request);

public class RtgsCommandHandler
{
    private readonly IRtgsRepository _rtgsRepository;
    private readonly IAccountRepository _accountRepository;

    public RtgsCommandHandler(
        IRtgsRepository rtgsRepository,
        IAccountRepository accountRepository)
    {
        _rtgsRepository = rtgsRepository;
        _accountRepository = accountRepository;
    }

    public async Task<RtgsResponseDto> HandleInitiateAsync(InitiateRtgsTransferCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var amount = Money.Bdt(req.Amount);
        var debtorAccNum = AccountNumber.From(req.DebtorAccount);

        var debtorAccount = await _accountRepository.GetByAccountNumberAsync(debtorAccNum, ct)
            ?? throw new KeyNotFoundException($"Debtor account '{req.DebtorAccount}' was not found.");

        // Debit debtor account for RTGS transfer
        debtorAccount.Debit(amount, $"RTGS Transfer to {req.CreditorName} (Ref: {req.EndToEndId})");
        await _accountRepository.UpdateAsync(debtorAccount, ct);

        var debtorRouting = RoutingNumber.From(req.DebtorAgentRouting);
        var creditorRouting = RoutingNumber.From(req.CreditorAgentRouting);

        var tx = new RtgsTransaction(
            Guid.NewGuid(),
            req.EndToEndId,
            req.MessageType,
            amount,
            debtorRouting,
            req.DebtorAccount,
            creditorRouting,
            req.CreditorAccount,
            req.CreditorName,
            req.RemittanceInfo,
            DateTime.UtcNow);

        await _rtgsRepository.AddAsync(tx, ct);

        return MapToDto(tx);
    }

    public async Task<RtgsResponseDto> HandleSettleAsync(SettleRtgsCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var tx = await _rtgsRepository.GetByEndToEndIdAsync(req.EndToEndId, ct)
            ?? throw new KeyNotFoundException($"RTGS transaction '{req.EndToEndId}' was not found.");

        tx.SettleAtBangladeshBank(req.BangladeshBankReference, DateTime.UtcNow);
        await _rtgsRepository.UpdateAsync(tx, ct);

        return MapToDto(tx);
    }

    public static RtgsResponseDto MapToDto(RtgsTransaction tx)
    {
        return new RtgsResponseDto(
            tx.Id,
            tx.EndToEndId,
            tx.MessageType,
            tx.Amount.Amount,
            tx.DebtorAccount,
            tx.CreditorAccount,
            tx.CreditorName,
            tx.Status,
            tx.BangladeshBankReference,
            tx.RejectionReason,
            tx.InitiatedAtUtc,
            tx.SettledAtUtc);
    }
}
