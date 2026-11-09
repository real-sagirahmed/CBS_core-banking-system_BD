using CBS.Application.Common.Interfaces;
using CBS.Application.PaymentRails.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.PaymentRails;
using CBS.Domain.ValueObjects;

namespace CBS.Application.PaymentRails.Commands.PresentCheque;

public record PresentOutwardChequeCommand(PresentChequeRequest Request);

public class PresentOutwardChequeCommandHandler
{
    private readonly IChequeClearingRepository _chequeRepository;

    public PresentOutwardChequeCommandHandler(IChequeClearingRepository chequeRepository)
    {
        _chequeRepository = chequeRepository;
    }

    public async Task<ChequeClearingResponseDto> HandleAsync(PresentOutwardChequeCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var routing = RoutingNumber.From(req.RoutingNumber);
        var acc = AccountNumber.From(req.AccountNumber);
        var micr = new MicrLine(req.ChequeNumber, routing, acc, req.TransactionCode);

        var presentingRouting = RoutingNumber.From(req.PresentingBankRouting);
        var draweeRouting = RoutingNumber.From(req.DraweeBankRouting);
        var amount = Money.Bdt(req.Amount);
        var presentationDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var item = new ChequeClearingItem(
            Guid.NewGuid(),
            req.BatchId,
            ClearingDirection.Outward,
            micr,
            amount,
            presentingRouting,
            draweeRouting,
            req.BeneficiaryAccountNumber,
            req.ChequeIssueDate,
            presentationDate);

        await _chequeRepository.AddAsync(item, ct);

        return new ChequeClearingResponseDto(
            item.Id,
            item.BatchId,
            item.Direction,
            item.Session,
            item.Micr.ChequeNumber,
            item.Micr.Routing.Value,
            item.Micr.Account.Value,
            item.Amount.Amount,
            item.Status,
            item.ReturnReason,
            item.ReturnRemarks,
            item.PresentationDate,
            item.SettlementDate);
    }
}
