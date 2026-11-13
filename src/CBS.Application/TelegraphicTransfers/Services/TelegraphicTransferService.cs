// ARCHITECTURAL INTENT: Telegraphic Transfer Application Service Implementation
// orchestrating fee calculation, advice transmission, telephone callback verification,
// test key validation, responding advice settlement, and aging reporting per BRPD Circular 12 (3.13, 1.6.5).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Application.TelegraphicTransfers.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.TelegraphicTransfers;
using CBS.Domain.ValueObjects;

namespace CBS.Application.TelegraphicTransfers.Services;

public class TelegraphicTransferService : ITelegraphicTransferService
{
    private readonly ITelegraphicTransferRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public TelegraphicTransferService(
        ITelegraphicTransferRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TelegraphicTransferResponseDto> IssueTtAsync(
        IssueTtRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ttId = $"TT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var ttNumber = $"TT-{request.IssuingBranchCode.Trim().ToUpperInvariant()}-{today:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

        var transfer = new TelegraphicTransfer(
            ttId: ttId,
            ttNumber: ttNumber,
            issueDate: today,
            issuingBranchCode: request.IssuingBranchCode,
            draweeBranchCode: request.DraweeBranchCode,
            amount: Money.Bdt(request.Amount),
            payeeName: request.PayeeName,
            payeeAccountNumber: request.PayeeAccountNumber,
            remitterName: request.RemitterName,
            remitterAccountNumber: request.RemitterAccountNumber,
            transactionPurpose: request.TransactionPurpose,
            sanctionScreeningPassed: request.SanctionScreeningPassed,
            makerUserId: request.MakerUserId,
            checkerUserId: request.CheckerUserId);

        await _repository.AddAsync(transfer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(transfer);
    }

    public async Task<TelegraphicTransferResponseDto> TransmitAdviceAsync(
        string ttId,
        TransmitTtAdviceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ttId);
        ArgumentNullException.ThrowIfNull(request);

        var transfer = await _repository.GetByIdAsync(ttId, cancellationToken)
            ?? throw new KeyNotFoundException($"Telegraphic Transfer '{ttId}' was not found.");

        transfer.MarkAdviceTransmitted(request.TransmittedByUserId);
        await _repository.UpdateAsync(transfer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(transfer);
    }

    public async Task<TelegraphicTransferResponseDto> LogTelephonicCallbackAsync(
        string ttId,
        LogTelephonicCallbackRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ttId);
        ArgumentNullException.ThrowIfNull(request);

        var transfer = await _repository.GetByIdAsync(ttId, cancellationToken)
            ?? throw new KeyNotFoundException($"Telegraphic Transfer '{ttId}' was not found.");

        transfer.LogTelephonicVerification(
            receivingOfficerUserId: request.ReceivingOfficerUserId,
            verifiedWithOfficerName: request.VerifiedWithOfficerName,
            contactNumber: request.ContactNumber,
            notes: request.Notes);

        await _repository.UpdateAsync(transfer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(transfer);
    }

    public async Task<TelegraphicTransferResponseDto> RespondAndPayTtAsync(
        string ttId,
        RespondAndPayTtRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ttId);
        ArgumentNullException.ThrowIfNull(request);

        var transfer = await _repository.GetByIdAsync(ttId, cancellationToken)
            ?? throw new KeyNotFoundException($"Telegraphic Transfer '{ttId}' was not found.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var voucher = $"VCH-TT-RESP-{today:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

        transfer.RespondAndPay(
            respondedDate: today,
            respondingOfficerUserId: request.RespondingOfficerUserId,
            respondingVoucherNumber: voucher,
            presentedTestKey: request.PresentedTestKey,
            destinationAccountOrSundryGl: request.DestinationAccountOrSundryGl);

        await _repository.UpdateAsync(transfer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(transfer);
    }

    public async Task<TelegraphicTransferResponseDto> CancelTtAsync(
        string ttId,
        CancelTtRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ttId);
        ArgumentNullException.ThrowIfNull(request);

        var transfer = await _repository.GetByIdAsync(ttId, cancellationToken)
            ?? throw new KeyNotFoundException($"Telegraphic Transfer '{ttId}' was not found.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        transfer.Cancel(today, request.CancelledByUserId, request.Remarks);

        await _repository.UpdateAsync(transfer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(transfer);
    }

    public async Task<TelegraphicTransferResponseDto?> GetByIdAsync(
        string ttId,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _repository.GetByIdAsync(ttId, cancellationToken);
        return transfer is not null ? MapToDto(transfer) : null;
    }

    public async Task<TelegraphicTransferResponseDto?> GetByTtNumberAsync(
        string ttNumber,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _repository.GetByTtNumberAsync(ttNumber, cancellationToken);
        return transfer is not null ? MapToDto(transfer) : null;
    }

    public async Task<IReadOnlyList<TelegraphicTransferResponseDto>> GetOutstandingTtAsync(
        string? branchCode,
        CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetOutstandingTtAsync(branchCode, cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task<UnrespondedTtAgingReportDto> GetUnrespondedTtAgingReportAsync(
        string? branchCode,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetUnrespondedTtAgingAsync(asOfDate, minimumDaysUnresponded: 0, cancellationToken);
        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var branch = branchCode.Trim().ToUpperInvariant();
            items = items.Where(i => i.IssuingBranchCode == branch || i.DraweeBranchCode == branch).ToList();
        }

        var agingItems = new List<UnrespondedTtAgingItemDto>();
        int over3Count = 0;
        decimal over3Amount = 0m;
        int over7Count = 0;
        decimal over7Amount = 0m;
        int over30Count = 0;
        decimal over30Amount = 0m;

        foreach (var tt in items)
        {
            var days = (asOfDate.ToDateTime(TimeOnly.MinValue) - tt.IssueDate.ToDateTime(TimeOnly.MinValue)).Days;
            string bucket;

            if (days >= 30)
            {
                bucket = "Over30Days_CriticalInterBranch";
                over30Count++;
                over30Amount += tt.Amount.Amount;
            }
            else if (days >= 7)
            {
                bucket = "Over7Days_Delayed";
                over7Count++;
                over7Amount += tt.Amount.Amount;
            }
            else if (days >= 3)
            {
                bucket = "Over3Days_Pending";
                over3Count++;
                over3Amount += tt.Amount.Amount;
            }
            else
            {
                bucket = "Within3Days_Normal";
            }

            agingItems.Add(new UnrespondedTtAgingItemDto(
                TtId: tt.Id,
                TtNumber: tt.TtNumber,
                IssuingBranchCode: tt.IssuingBranchCode,
                DraweeBranchCode: tt.DraweeBranchCode,
                Amount: tt.Amount.Amount,
                PayeeName: tt.PayeeName,
                IssueDate: tt.IssueDate,
                DaysUnresponded: days,
                Status: tt.Status.ToString(),
                AgingBucket: bucket));
        }

        return new UnrespondedTtAgingReportDto(
            AsOfDate: asOfDate,
            BranchCode: branchCode,
            TotalUnrespondedCount: agingItems.Count,
            TotalUnrespondedAmount: agingItems.Sum(x => x.Amount),
            Over3DaysCount: over3Count,
            Over3DaysAmount: over3Amount,
            Over7DaysCount: over7Count,
            Over7DaysAmount: over7Amount,
            Over30DaysCount: over30Count,
            Over30DaysAmount: over30Amount,
            Items: agingItems);
    }

    private static TelegraphicTransferResponseDto MapToDto(TelegraphicTransfer tt) =>
        new(
            TtId: tt.Id,
            TtNumber: tt.TtNumber,
            IssueDate: tt.IssueDate,
            IssuingBranchCode: tt.IssuingBranchCode,
            DraweeBranchCode: tt.DraweeBranchCode,
            Amount: tt.Amount.Amount,
            Currency: tt.Amount.Currency.Code,
            PayeeName: tt.PayeeName,
            PayeeAccountNumber: tt.PayeeAccountNumber,
            RemitterName: tt.RemitterName,
            RemitterAccountNumber: tt.RemitterAccountNumber,
            TransactionPurpose: tt.TransactionPurpose,
            CommissionAmount: tt.CommissionAmount,
            MessageChargeAmount: tt.MessageChargeAmount,
            VatAmount: tt.VatAmount,
            TotalDebitedFromRemitter: tt.Amount.Amount + tt.CommissionAmount + tt.MessageChargeAmount + tt.VatAmount,
            TtTestKey: tt.TtTestKey,
            Status: tt.Status.ToString(),
            SanctionScreeningPassed: tt.SanctionScreeningPassed,
            MakerUserId: tt.MakerUserId,
            CheckerUserId: tt.CheckerUserId,
            AdviceTransmittedAtUtc: tt.AdviceTransmittedAtUtc,
            AdviceTransmittedByUserId: tt.AdviceTransmittedByUserId,
            TelephonicallyVerifiedAtUtc: tt.TelephonicallyVerifiedAtUtc,
            TelephonicVerifiedByOfficer: tt.TelephonicVerifiedByOfficer,
            TelephonicContactNumber: tt.TelephonicContactNumber,
            TelephonicNotes: tt.TelephonicNotes,
            RespondedDate: tt.RespondedDate,
            RespondingAdviceNumber: tt.RespondingAdviceNumber,
            RespondingOfficerUserId: tt.RespondingOfficerUserId,
            RespondingVoucherNumber: tt.RespondingVoucherNumber,
            DestinationAccountOrSundryGl: tt.DestinationAccountOrSundryGl,
            CancelledDate: tt.CancelledDate,
            CancelledByUserId: tt.CancelledByUserId,
            CancellationRemarks: tt.CancellationRemarks,
            CreatedAtUtc: tt.CreatedAtUtc);
}
