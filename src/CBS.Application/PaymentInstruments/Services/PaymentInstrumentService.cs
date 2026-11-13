// ARCHITECTURAL INTENT: Payment Instrument Application Service Implementation
// orchestrating physical stock leaf allocation, fee/VAT accounting, cryptographic test keys,
// and unpresented aging per Bangladesh Bank BRPD Circular No. 12 (1.5.6, 3.11, 3.12).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Application.PaymentInstruments.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.PaymentInstruments;
using CBS.Domain.ValueObjects;

namespace CBS.Application.PaymentInstruments.Services;

public class PaymentInstrumentService : IPaymentInstrumentService
{
    private readonly IPaymentInstrumentRepository _instrumentRepository;
    private readonly IPaymentInstrumentStockRepository _stockRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentInstrumentService(
        IPaymentInstrumentRepository instrumentRepository,
        IPaymentInstrumentStockRepository stockRepository,
        IUnitOfWork unitOfWork)
    {
        _instrumentRepository = instrumentRepository ?? throw new ArgumentNullException(nameof(instrumentRepository));
        _stockRepository = stockRepository ?? throw new ArgumentNullException(nameof(stockRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<PaymentInstrumentStockBookDto> AllocateStockBookAsync(
        AllocateStockBookRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bookId = $"STK-{request.BranchCode}-{request.InstrumentType}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        var stockBook = new PaymentInstrumentStockBook(
            bookId: bookId,
            branchCode: request.BranchCode,
            instrumentType: request.InstrumentType,
            prefix: request.Prefix,
            startLeafNumber: request.StartLeafNumber,
            totalLeaves: request.TotalLeaves,
            custodianUserId: request.CustodianUserId,
            allocatedByUserId: request.AllocatedByUserId);

        await _stockRepository.AddAsync(stockBook, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapStockBookToDto(stockBook);
    }

    public async Task<PaymentInstrumentResponseDto> IssuePayOrderAsync(
        IssuePayOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stockBook = await GetOrCreateActiveStockBookAsync(
            request.IssuingBranchCode,
            PaymentInstrumentType.PayOrder,
            $"PO-{request.IssuingBranchCode}",
            request.MakerUserId,
            request.CheckerUserId,
            cancellationToken);

        var leafNumber = stockBook.AllocateNextLeaf();
        var instrumentId = $"PI-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        var instrument = new PaymentInstrument(
            instrumentId: instrumentId,
            instrumentType: PaymentInstrumentType.PayOrder,
            instrumentNumber: leafNumber,
            issuingBranchCode: request.IssuingBranchCode,
            draweeBranchCode: request.IssuingBranchCode,
            amount: Money.Bdt(request.Amount),
            payeeName: request.PayeeName,
            remitterName: request.RemitterName,
            remitterAccountNumber: request.RemitterAccountNumber,
            isWalkInCustomer: request.IsWalkInCustomer,
            walkInNid: request.WalkInNid,
            walkInPhotoId: request.WalkInPhotoId,
            walkInMobile: request.WalkInMobile,
            transactionPurpose: request.TransactionPurpose,
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            issuedByUserId: request.MakerUserId,
            authorizedByUserId: request.CheckerUserId);

        await _instrumentRepository.AddAsync(instrument, cancellationToken);
        await _stockRepository.UpdateAsync(stockBook, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapInstrumentToDto(instrument);
    }

    public async Task<PaymentInstrumentResponseDto> IssueDemandDraftAsync(
        IssueDemandDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stockBook = await GetOrCreateActiveStockBookAsync(
            request.IssuingBranchCode,
            PaymentInstrumentType.DemandDraft,
            $"DD-{request.IssuingBranchCode}",
            request.MakerUserId,
            request.CheckerUserId,
            cancellationToken);

        var leafNumber = stockBook.AllocateNextLeaf();
        var instrumentId = $"PI-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        var instrument = new PaymentInstrument(
            instrumentId: instrumentId,
            instrumentType: PaymentInstrumentType.DemandDraft,
            instrumentNumber: leafNumber,
            issuingBranchCode: request.IssuingBranchCode,
            draweeBranchCode: request.DraweeBranchCode,
            amount: Money.Bdt(request.Amount),
            payeeName: request.PayeeName,
            remitterName: request.RemitterName,
            remitterAccountNumber: request.RemitterAccountNumber,
            isWalkInCustomer: request.IsWalkInCustomer,
            walkInNid: request.WalkInNid,
            walkInPhotoId: request.WalkInPhotoId,
            walkInMobile: request.WalkInMobile,
            transactionPurpose: request.TransactionPurpose,
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            issuedByUserId: request.MakerUserId,
            authorizedByUserId: request.CheckerUserId);

        await _instrumentRepository.AddAsync(instrument, cancellationToken);
        await _stockRepository.UpdateAsync(stockBook, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapInstrumentToDto(instrument);
    }

    public async Task<PaymentInstrumentResponseDto> EncashInstrumentAsync(
        string instrumentId,
        EncashPaymentInstrumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instrumentId);
        ArgumentNullException.ThrowIfNull(request);

        var instrument = await _instrumentRepository.GetByIdAsync(instrumentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment instrument '{instrumentId}' was not found.");

        var voucherNo = $"VCH-ENC-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        instrument.MarkPaid(
            paidDate: DateOnly.FromDateTime(DateTime.UtcNow),
            paidByUserId: request.PaidByUserId,
            voucherNumber: voucherNo,
            presentedTestKey: request.PresentedTestKey);

        await _instrumentRepository.UpdateAsync(instrument, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapInstrumentToDto(instrument);
    }

    public async Task<PaymentInstrumentResponseDto> CancelInstrumentAsync(
        string instrumentId,
        CancelPaymentInstrumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instrumentId);
        ArgumentNullException.ThrowIfNull(request);

        var instrument = await _instrumentRepository.GetByIdAsync(instrumentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment instrument '{instrumentId}' was not found.");

        instrument.Cancel(
            cancelledDate: DateOnly.FromDateTime(DateTime.UtcNow),
            cancelledByUserId: request.CancelledByUserId,
            remarks: request.Remarks,
            cancellationFee: request.CancellationFee);

        await _instrumentRepository.UpdateAsync(instrument, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapInstrumentToDto(instrument);
    }

    public async Task<PaymentInstrumentResponseDto> IssueDuplicateAsync(
        IssueDuplicateInstrumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var original = await _instrumentRepository.GetByIdAsync(request.OriginalInstrumentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Original payment instrument '{request.OriginalInstrumentId}' was not found.");

        var stockBook = await GetOrCreateActiveStockBookAsync(
            original.IssuingBranchCode,
            original.InstrumentType,
            $"{original.InstrumentType.ToString()[..2].ToUpperInvariant()}-{original.IssuingBranchCode}",
            request.MakerUserId,
            request.CheckerUserId,
            cancellationToken);

        var newLeafNumber = stockBook.AllocateNextLeaf();
        var newInstrumentId = $"PI-DUP-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        var duplicate = PaymentInstrument.CreateDuplicate(
            newInstrumentId: newInstrumentId,
            newInstrumentNumber: newLeafNumber,
            original: original,
            policeGdNumber: request.PoliceGdNumber,
            indemnityBondNumber: request.IndemnityBondNumber,
            issuedByUserId: request.MakerUserId,
            authorizedByUserId: request.CheckerUserId);

        await _instrumentRepository.UpdateAsync(original, cancellationToken);
        await _instrumentRepository.AddAsync(duplicate, cancellationToken);
        await _stockRepository.UpdateAsync(stockBook, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapInstrumentToDto(duplicate);
    }

    public async Task<IReadOnlyList<PaymentInstrumentResponseDto>> GetOutstandingInstrumentsAsync(
        string? branchCode,
        PaymentInstrumentType? type,
        CancellationToken cancellationToken = default)
    {
        var list = await _instrumentRepository.GetOutstandingInstrumentsAsync(branchCode, type, cancellationToken);
        return list.Select(MapInstrumentToDto).ToList();
    }

    public async Task<UnpresentedAgingReportDto> GetUnpresentedAgingReportAsync(
        string? branchCode,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var items = await _instrumentRepository.GetUnpresentedInstrumentsAsync(asOfDate, minimumDaysUnpresented: 0, cancellationToken);
        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            items = items.Where(i => string.Equals(i.IssuingBranchCode, branchCode.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var agingItems = new List<UnpresentedAgingItemDto>();
        int stale6mCount = 0;
        decimal stale6mAmount = 0m;
        int unclaimed3yCount = 0;
        decimal unclaimed3yAmount = 0m;
        int bb10yCount = 0;
        decimal bb10yAmount = 0m;

        foreach (var inst in items)
        {
            var days = (asOfDate.ToDateTime(TimeOnly.MinValue) - inst.IssueDate.ToDateTime(TimeOnly.MinValue)).Days;
            string bucket;

            if (days >= 3650)
            {
                bucket = "Over10Years_BangladeshBankTransferEligible";
                bb10yCount++;
                bb10yAmount += inst.Amount.Amount;
            }
            else if (days >= 1095)
            {
                bucket = "Over3Years_Unclaimed";
                unclaimed3yCount++;
                unclaimed3yAmount += inst.Amount.Amount;
            }
            else if (days >= 180)
            {
                bucket = "Over6Months_Stale";
                stale6mCount++;
                stale6mAmount += inst.Amount.Amount;
            }
            else
            {
                bucket = "Within6Months_Active";
            }

            agingItems.Add(new UnpresentedAgingItemDto(
                InstrumentId: inst.Id,
                InstrumentType: inst.InstrumentType.ToString(),
                InstrumentNumber: inst.InstrumentNumber,
                IssuingBranchCode: inst.IssuingBranchCode,
                Amount: inst.Amount.Amount,
                PayeeName: inst.PayeeName,
                RemitterName: inst.RemitterName,
                IssueDate: inst.IssueDate,
                DaysUnpresented: days,
                AgingBucket: bucket));
        }

        return new UnpresentedAgingReportDto(
            AsOfDate: asOfDate,
            BranchCode: branchCode,
            TotalUnpresentedCount: agingItems.Count,
            TotalUnpresentedAmount: agingItems.Sum(x => x.Amount),
            StaleCountOver6Months: stale6mCount,
            StaleAmountOver6Months: stale6mAmount,
            UnclaimedCountOver3Years: unclaimed3yCount,
            UnclaimedAmountOver3Years: unclaimed3yAmount,
            BangladeshBankTransferEligibleOver10Years: bb10yCount,
            BangladeshBankTransferEligibleAmount: bb10yAmount,
            Items: agingItems);
    }

    private async Task<PaymentInstrumentStockBook> GetOrCreateActiveStockBookAsync(
        string branchCode,
        PaymentInstrumentType type,
        string prefix,
        string makerUserId,
        string checkerUserId,
        CancellationToken cancellationToken)
    {
        var existing = await _stockRepository.GetActiveBookForBranchAsync(branchCode, type, cancellationToken);
        if (existing is not null && !existing.IsExhausted)
        {
            return existing;
        }

        // Auto-provision initial joint-custody stock book if none exists
        var bookId = $"STK-{branchCode}-{type}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        var newBook = new PaymentInstrumentStockBook(
            bookId: bookId,
            branchCode: branchCode,
            instrumentType: type,
            prefix: prefix,
            startLeafNumber: 1,
            totalLeaves: 100,
            custodianUserId: checkerUserId,
            allocatedByUserId: makerUserId);

        await _stockRepository.AddAsync(newBook, cancellationToken);
        return newBook;
    }

    public async Task<PaymentInstrumentResponseDto?> GetInstrumentByIdAsync(
        string instrumentId,
        CancellationToken cancellationToken = default)
    {
        var inst = await _instrumentRepository.GetByIdAsync(instrumentId, cancellationToken);
        return inst is not null ? MapInstrumentToDto(inst) : null;
    }

    public async Task<PaymentInstrumentResponseDto?> GetInstrumentByNumberAsync(
        string instrumentNumber,
        CancellationToken cancellationToken = default)
    {
        var inst = await _instrumentRepository.GetByInstrumentNumberAsync(instrumentNumber, cancellationToken);
        return inst is not null ? MapInstrumentToDto(inst) : null;
    }

    private static PaymentInstrumentStockBookDto MapStockBookToDto(PaymentInstrumentStockBook book) =>
        new(
            BookId: book.Id,
            BranchCode: book.BranchCode,
            InstrumentType: book.InstrumentType.ToString(),
            Prefix: book.Prefix,
            StartLeafNumber: book.StartLeafNumber,
            EndLeafNumber: book.EndLeafNumber,
            TotalLeaves: book.TotalLeaves,
            IssuedLeavesCount: book.IssuedLeavesCount,
            NextAvailableLeafNumber: book.NextAvailableLeafNumber,
            CustodianUserId: book.CustodianUserId,
            AllocatedByUserId: book.AllocatedByUserId,
            IsExhausted: book.IsExhausted,
            AllocatedAtUtc: book.AllocatedAtUtc);

    private static PaymentInstrumentResponseDto MapInstrumentToDto(PaymentInstrument inst) =>
        new(
            InstrumentId: inst.Id,
            InstrumentType: inst.InstrumentType.ToString(),
            InstrumentNumber: inst.InstrumentNumber,
            IssuingBranchCode: inst.IssuingBranchCode,
            DraweeBranchCode: inst.DraweeBranchCode,
            Amount: inst.Amount.Amount,
            Currency: inst.Amount.Currency.Code,
            PayeeName: inst.PayeeName,
            RemitterName: inst.RemitterName,
            RemitterAccountNumber: inst.RemitterAccountNumber,
            IsWalkInCustomer: inst.IsWalkInCustomer,
            WalkInNid: inst.WalkInNid,
            WalkInMobile: inst.WalkInMobile,
            TransactionPurpose: inst.TransactionPurpose,
            Status: inst.Status.ToString(),
            CommissionAmount: inst.CommissionAmount,
            VatAmount: inst.VatAmount,
            TotalDebitedFromCustomer: inst.Amount.Amount + inst.CommissionAmount + inst.VatAmount,
            IssueDate: inst.IssueDate,
            IssuedByUserId: inst.IssuedByUserId,
            AuthorizedByUserId: inst.AuthorizedByUserId,
            DdTestKey: inst.DdTestKey,
            DrawingAdviceTransmitted: inst.DrawingAdviceTransmitted,
            PaidDate: inst.PaidDate,
            PaidByUserId: inst.PaidByUserId,
            PaidVoucherNumber: inst.PaidVoucherNumber,
            CancelledDate: inst.CancelledDate,
            CancelledByUserId: inst.CancelledByUserId,
            CancellationRemarks: inst.CancellationRemarks,
            CancellationFeeDeducted: inst.CancellationFeeDeducted,
            PoliceGdNumber: inst.PoliceGdNumber,
            IndemnityBondNumber: inst.IndemnityBondNumber,
            DuplicateOfInstrumentId: inst.DuplicateOfInstrumentId,
            CreatedAtUtc: inst.CreatedAtUtc);
}
