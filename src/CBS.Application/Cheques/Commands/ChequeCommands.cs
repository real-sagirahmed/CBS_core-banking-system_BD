using CBS.Application.Cheques.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Domain.Cheques;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Cheques.Commands;

public sealed record RequisitionChequeBookCommand(RequisitionChequeBookRequest Request);
public sealed record DeliverChequeBookCommand(string BookId);
public sealed record StopPaymentCommand(ChequeStopPaymentRequest Request);
public sealed record RevokeStopPaymentCommand(ChequeRevokeStopPaymentRequest Request);

public class ChequeCommandHandler
{
    private readonly IChequeBookRepository _chequeRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public ChequeCommandHandler(
        IChequeBookRepository chequeRepository,
        IAccountRepository accountRepository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _chequeRepository = chequeRepository ?? throw new ArgumentNullException(nameof(chequeRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _glService = glService ?? throw new ArgumentNullException(nameof(glService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ChequeBookResponseDto> HandleRequisitionAsync(RequisitionChequeBookCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var accNo = AccountNumber.From(req.AccountNumber);

        var account = await _accountRepository.GetByAccountNumberAsync(accNo, ct)
            ?? throw new KeyNotFoundException($"Customer account '{req.AccountNumber}' not found.");

        // BRPD 1.5.7-vi & 3.1.14-iii: Charge per leaf (BDT 5 per leaf + 15% NBR VAT)
        decimal chargePerLeaf = 5.00m;
        decimal baseCharge = chargePerLeaf * req.TotalLeaves;
        decimal vatAmount = Math.Round(baseCharge * 0.15m, 2, MidpointRounding.AwayFromZero);
        decimal totalFee = baseCharge + vatAmount;

        // Deduct charge from account
        account.Debit(Money.Bdt(totalFee), $"Cheque Book Issuance Fee ({req.TotalLeaves} leaves + 15% VAT)", isCustomerInduced: true);
        await _accountRepository.UpdateAsync(account, ct);

        // Post to GL
        await _glService.PostPaymentRailTransferAsync(
            branchSol: req.BranchCode,
            originatingAccount: account.Id.Value,
            amount: totalFee,
            railType: "CHEQUE_BOOK_ISSUANCE",
            refId: req.BookId,
            makerUserId: "SYSTEM_CHEQUE_ENGINE",
            checkerUserId: null,
            ct: ct);

        var book = new ChequeBook(
            bookId: req.BookId,
            accountNumber: accNo,
            prefix: req.Prefix,
            startLeafNumber: req.StartLeafNumber,
            totalLeaves: req.TotalLeaves,
            branchCode: req.BranchCode,
            requisitionDate: DateOnly.FromDateTime(DateTime.UtcNow));

        await _chequeRepository.AddBookAsync(book, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ChequeBookResponseDto(
            book.Id,
            book.AccountNumber.Value,
            book.Prefix,
            book.StartLeafNumber,
            book.EndLeafNumber,
            book.TotalLeaves,
            book.Status.ToString(),
            book.BranchCode,
            book.RequisitionDate,
            book.DeliveredAtUtc);
    }

    public async Task<ChequeBookResponseDto> HandleDeliverBookAsync(DeliverChequeBookCommand command, CancellationToken ct = default)
    {
        var book = await _chequeRepository.GetBookByIdAsync(command.BookId, ct)
            ?? throw new KeyNotFoundException($"Cheque book '{command.BookId}' not found.");

        book.MarkDelivered();
        await _chequeRepository.UpdateBookAsync(book, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ChequeBookResponseDto(
            book.Id,
            book.AccountNumber.Value,
            book.Prefix,
            book.StartLeafNumber,
            book.EndLeafNumber,
            book.TotalLeaves,
            book.Status.ToString(),
            book.BranchCode,
            book.RequisitionDate,
            book.DeliveredAtUtc);
    }

    public async Task<ChequeLeafStatusDto> HandleStopPaymentAsync(StopPaymentCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var accNo = AccountNumber.From(req.AccountNumber);

        var leaf = await _chequeRepository.GetLeafAsync(req.AccountNumber, req.LeafNumber, ct)
            ?? throw new KeyNotFoundException($"Cheque leaf '{req.LeafNumber}' for account '{req.AccountNumber}' not found.");

        var book = await _chequeRepository.GetBookByIdAsync(leaf.BookId, ct)
            ?? throw new KeyNotFoundException($"Cheque book '{leaf.BookId}' not found.");

        var account = await _accountRepository.GetByAccountNumberAsync(accNo, ct)
            ?? throw new KeyNotFoundException($"Customer account '{req.AccountNumber}' not found.");

        // BRPD 1.5.7-vi & ix: Stop payment fee (BDT 100 + 15% VAT = 115 BDT)
        decimal stopFee = 115.00m;
        if (account.Balance.Amount >= stopFee)
        {
            account.Debit(Money.Bdt(stopFee), $"Stop payment fee for Cheque Leaf #{req.LeafNumber}", isCustomerInduced: true);
            await _accountRepository.UpdateAsync(account, ct);

            await _glService.PostPaymentRailTransferAsync(
                branchSol: req.BranchCode,
                originatingAccount: account.Id.Value,
                amount: stopFee,
                railType: "CHEQUE_STOP_PAYMENT",
                refId: $"STOP-{req.LeafNumber}",
                makerUserId: req.OfficerId,
                checkerUserId: null,
                ct: ct);
        }

        book.StopLeaf(req.LeafNumber, req.Reason, req.Notes, req.BranchCode);
        await _chequeRepository.UpdateBookAsync(book, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ChequeLeafStatusDto(
            leaf.Id,
            leaf.BookId,
            leaf.AccountNumber.Value,
            leaf.LeafNumber,
            leaf.Status.ToString(),
            leaf.StopReason?.ToString(),
            leaf.StopNotes,
            leaf.StoppedAtUtc,
            leaf.StoppedByBranch,
            leaf.PaidAtUtc,
            leaf.PaidVoucherNumber);
    }

    public async Task<ChequeLeafStatusDto> HandleRevokeStopPaymentAsync(RevokeStopPaymentCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var leaf = await _chequeRepository.GetLeafAsync(req.AccountNumber, req.LeafNumber, ct)
            ?? throw new KeyNotFoundException($"Cheque leaf '{req.LeafNumber}' for account '{req.AccountNumber}' not found.");

        var book = await _chequeRepository.GetBookByIdAsync(leaf.BookId, ct)
            ?? throw new KeyNotFoundException($"Cheque book '{leaf.BookId}' not found.");

        book.RevokeStopPayment(req.LeafNumber, req.OfficerId, req.Reason);
        await _chequeRepository.UpdateBookAsync(book, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ChequeLeafStatusDto(
            leaf.Id,
            leaf.BookId,
            leaf.AccountNumber.Value,
            leaf.LeafNumber,
            leaf.Status.ToString(),
            leaf.StopReason?.ToString(),
            leaf.StopNotes,
            leaf.StoppedAtUtc,
            leaf.StoppedByBranch,
            leaf.PaidAtUtc,
            leaf.PaidVoucherNumber);
    }

    public async Task<ChequeLeafStatusDto?> HandleGetLeafStatusAsync(string accountNumber, long leafNumber, CancellationToken ct = default)
    {
        var leaf = await _chequeRepository.GetLeafAsync(accountNumber, leafNumber, ct);
        if (leaf is null) return null;

        return new ChequeLeafStatusDto(
            leaf.Id,
            leaf.BookId,
            leaf.AccountNumber.Value,
            leaf.LeafNumber,
            leaf.Status.ToString(),
            leaf.StopReason?.ToString(),
            leaf.StopNotes,
            leaf.StoppedAtUtc,
            leaf.StoppedByBranch,
            leaf.PaidAtUtc,
            leaf.PaidVoucherNumber);
    }

    public async Task<IEnumerable<UndeliveredChequeReportItemDto>> HandleGetUndeliveredBooksReportAsync(DateOnly asOfDate, CancellationToken ct = default)
    {
        var books = await _chequeRepository.GetUndeliveredBooksAsync(asOfDate, ct);
        return books.Select(b => new UndeliveredChequeReportItemDto(
            b.Id,
            b.AccountNumber.Value,
            b.BranchCode,
            b.RequisitionDate,
            asOfDate.DayNumber - b.RequisitionDate.DayNumber));
    }
}
