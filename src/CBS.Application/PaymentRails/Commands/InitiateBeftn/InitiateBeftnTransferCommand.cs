using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
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
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public InitiateBeftnTransferCommandHandler(
        IBeftnRepository beftnRepository,
        IAccountRepository accountRepository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _beftnRepository = beftnRepository ?? throw new ArgumentNullException(nameof(beftnRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _glService = glService ?? throw new ArgumentNullException(nameof(glService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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

        // Auto-post balanced BEFTN Payment Rail GL voucher (BRPD Module 1.3)
        await _glService.PostPaymentRailTransferAsync(
            origAccount.BranchCode,
            req.OriginatingAccount,
            req.Amount,
            "BEFTN",
            req.BatchId,
            "SYSTEM_BEFTN",
            null,
            ct);

        await _unitOfWork.SaveChangesAsync(ct);

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
