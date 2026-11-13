// ARCHITECTURAL INTENT: Back-Value Governance Service orchestrating Value-Dated Transaction approvals,
// Retroactive Interest Differentials, Linked Cascade Reversals, and EOD Supervisory Audit Journals
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 1 (1.3.4 & 1.3.6).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.BackValue.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.BackValue;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.BackValue.Services;

public class BackValueGovernanceService : IBackValueGovernanceService
{
    private readonly IBackValueTransactionRepository _backValueRepository;
    private readonly IBackValueAuditJournalRepository _journalRepository;
    private readonly ILinkedReversalRepository _reversalRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BackValueGovernanceService(
        IBackValueTransactionRepository backValueRepository,
        IBackValueAuditJournalRepository journalRepository,
        ILinkedReversalRepository reversalRepository,
        IUnitOfWork unitOfWork)
    {
        _backValueRepository = backValueRepository;
        _journalRepository = journalRepository;
        _reversalRepository = reversalRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<BackValueTransactionResponseDto> SubmitBackValueRequestAsync(SubmitBackValueTransactionRequest request, CancellationToken ct = default)
    {
        var requestId = $"BV-REQ-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var entity = new BackValueTransactionRequest(
            requestId: requestId,
            targetType: request.TargetType,
            targetIdentifier: request.TargetIdentifier,
            branchCode: request.BranchCode,
            bookingDate: request.BookingDate,
            valueDate: request.ValueDate,
            amount: Money.Bdt(request.Amount),
            transactionNature: request.TransactionNature,
            reason: request.Reason,
            regulatoryReference: request.RegulatoryReference,
            makerUserId: request.MakerUserId);

        await _backValueRepository.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task<BackValueTransactionResponseDto> ApproveAndExecuteBackValueAsync(string requestId, ApproveBackValueTransactionRequest request, CancellationToken ct = default)
    {
        var backValueReq = await _backValueRepository.GetByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException($"Back-value transaction request '{requestId}' was not found.");

        // Calculate retroactive interest differential (default 6% if not otherwise configured)
        decimal defaultRate = 6.00m;
        var retroResult = RetroactiveInterestCalculator.Calculate(
            principalAmount: backValueReq.Amount.Amount,
            annualInterestRatePercent: defaultRate,
            valueDate: backValueReq.ValueDate,
            bookingDate: backValueReq.BookingDate,
            transactionNature: backValueReq.TransactionNature,
            isDepositAccount: backValueReq.TargetType == BackValueTargetType.DepositAccount);

        var executedVoucherNo = $"VCH-BV-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        string? retroVoucherNo = retroResult.DifferentialAmount > 0
            ? $"VCH-INTR-{Guid.NewGuid():N}"[..14].ToUpperInvariant()
            : null;

        backValueReq.Approve(
            checkerUserId: request.CheckerUserId,
            remarks: request.Remarks,
            executedVoucherNumber: executedVoucherNo,
            retroactiveInterestCalculated: retroResult.DifferentialAmount,
            retroactiveInterestVoucherNumber: retroVoucherNo);

        // Record entry into Supervisory EOD Back-Value Audit Journal (BRPD 1.3.4-iii)
        var journalEntryId = $"BV-JRN-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var journal = new BackValueAuditJournalEntry(
            entryId: journalEntryId,
            requestId: backValueReq.RequestId,
            branchCode: backValueReq.BranchCode,
            bookingDate: backValueReq.BookingDate,
            valueDate: backValueReq.ValueDate,
            daysVariance: backValueReq.DaysVariance,
            targetAccountOrGl: backValueReq.TargetIdentifier,
            principalAmount: backValueReq.Amount.Amount,
            currency: backValueReq.Amount.Currency.Code,
            retroactiveInterestAmount: retroResult.DifferentialAmount,
            direction: retroResult.Direction,
            makerUserId: backValueReq.MakerUserId,
            checkerUserId: request.CheckerUserId,
            approvalTimeUtc: DateTime.UtcNow,
            voucherNumber: executedVoucherNo,
            auditNarration: $"Approved back-value entry ({backValueReq.DaysVariance} days). {retroResult.AccountingNarration}");

        await _journalRepository.AddJournalEntryAsync(journal, ct);
        await _backValueRepository.UpdateAsync(backValueReq, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(backValueReq);
    }

    public async Task<BackValueTransactionResponseDto> RejectBackValueRequestAsync(string requestId, RejectBackValueTransactionRequest request, CancellationToken ct = default)
    {
        var backValueReq = await _backValueRepository.GetByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException($"Back-value transaction request '{requestId}' was not found.");

        backValueReq.Reject(request.CheckerUserId, request.RejectionReason);
        await _backValueRepository.UpdateAsync(backValueReq, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(backValueReq);
    }

    public async Task<IReadOnlyList<BackValueTransactionResponseDto>> GetPendingBackValueRequestsAsync(CancellationToken ct = default)
    {
        var list = await _backValueRepository.GetPendingRequestsAsync(ct);
        return list.Select(MapToDto).ToList();
    }

    public RetroactiveInterestCalculationResultDto SimulateRetroactiveInterest(SimulateRetroactiveInterestRequest request)
    {
        var result = RetroactiveInterestCalculator.Calculate(
            principalAmount: request.PrincipalAmount,
            annualInterestRatePercent: request.AnnualInterestRatePercent,
            valueDate: request.ValueDate,
            bookingDate: request.BookingDate,
            transactionNature: request.TransactionNature,
            isDepositAccount: request.IsDepositAccount);

        return new RetroactiveInterestCalculationResultDto(
            DifferentialAmount: result.DifferentialAmount,
            Direction: result.Direction.ToString(),
            DaysPast: result.DaysPast,
            PrincipalAmount: result.PrincipalAmount,
            AnnualInterestRatePercent: result.AnnualInterestRatePercent,
            AccountingNarration: result.AccountingNarration);
    }

    public async Task<CascadeReversalResponseDto> ExecuteCascadeReversalAsync(ReverseTransactionCascadeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.OriginalVoucherId) || string.IsNullOrWhiteSpace(request.OriginalVoucherNumber))
            throw new ArgumentException("Original voucher ID and voucher number are required.");

        if (string.IsNullOrWhiteSpace(request.ReversedByUserId) || string.IsNullOrWhiteSpace(request.AuthorizerUserId))
            throw new ArgumentException("Maker and authorizer user IDs are required for reversal.");

        // Maker cannot approve own reversal
        if (string.Equals(request.ReversedByUserId, request.AuthorizerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "ReversalMakerCheckerInvariant",
                "BRPD Circular 12/2024 (1.3.13, 1.3.6)",
                $"Maker '{request.ReversedByUserId}' cannot authorize their own transaction reversal. Independent checker authorization required.");
        }

        var parentReversalVoucherNo = $"REV-{request.OriginalVoucherNumber}";
        var linkedList = request.LinkedAuxiliaryEntries ?? new List<LinkedAuxiliaryEntryDto>();
        var linkedJson = JsonSerializer.Serialize(linkedList);

        var reversalRecordId = $"REV-REC-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var record = new LinkedTransactionReversalRecord(
            reversalRecordId: reversalRecordId,
            originalVoucherId: request.OriginalVoucherId,
            originalVoucherNumber: request.OriginalVoucherNumber,
            reversalReason: request.ReversalReason,
            reversedByUserId: request.ReversedByUserId,
            authorizerUserId: request.AuthorizerUserId,
            parentReversalVoucherNumber: parentReversalVoucherNo,
            linkedReversalsJson: linkedJson,
            totalReversedAmount: Money.Bdt(request.TotalReversedAmount));

        await _reversalRepository.AddReversalRecordAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new CascadeReversalResponseDto(
            ReversalRecordId: record.ReversalRecordId,
            OriginalVoucherId: record.OriginalVoucherId,
            OriginalVoucherNumber: record.OriginalVoucherNumber,
            ParentReversalVoucherNumber: record.ParentReversalVoucherNumber,
            TotalReversedAmount: record.TotalReversedAmount.Amount,
            LinkedEntriesReversedCount: linkedList.Count,
            ReversedByUserId: record.ReversedByUserId,
            AuthorizerUserId: record.AuthorizerUserId,
            ReversedAtUtc: record.ReversedAtUtc,
            IsCascadeComplete: record.IsCascadeComplete);
    }

    public async Task<EodBackValueJournalReportDto> GetEodBackValueJournalReportAsync(DateOnly operatingDate, string? branchCode = null, CancellationToken ct = default)
    {
        var entries = await _journalRepository.GetJournalEntriesByDateAsync(operatingDate, branchCode, ct);

        var dtoList = entries.Select(e => new BackValueAuditJournalEntryDto(
            EntryId: e.EntryId,
            RequestId: e.RequestId,
            BranchCode: e.BranchCode,
            BookingDate: e.BookingDate,
            ValueDate: e.ValueDate,
            DaysVariance: e.DaysVariance,
            TargetAccountOrGl: e.TargetAccountOrGl,
            PrincipalAmount: e.PrincipalAmount,
            Currency: e.Currency,
            RetroactiveInterestAmount: e.RetroactiveInterestAmount,
            Direction: e.Direction.ToString(),
            MakerUserId: e.MakerUserId,
            CheckerUserId: e.CheckerUserId,
            ApprovalTimeUtc: e.ApprovalTimeUtc,
            VoucherNumber: e.VoucherNumber,
            AuditNarration: e.AuditNarration)).ToList();

        decimal totalPrincipal = dtoList.Sum(x => x.PrincipalAmount);
        decimal totalRetroInterest = dtoList.Sum(x => x.RetroactiveInterestAmount);

        return new EodBackValueJournalReportDto(
            OperatingDate: operatingDate,
            BranchCode: string.IsNullOrWhiteSpace(branchCode) ? "ALL" : branchCode.Trim().ToUpperInvariant(),
            TotalBackValueEntriesCount: dtoList.Count,
            TotalPrincipalAmount: totalPrincipal,
            TotalRetroactiveInterestAccrued: totalRetroInterest,
            JournalEntries: dtoList);
    }

    private static BackValueTransactionResponseDto MapToDto(BackValueTransactionRequest e) =>
        new(
            RequestId: e.RequestId,
            TargetType: e.TargetType.ToString(),
            TargetIdentifier: e.TargetIdentifier,
            BranchCode: e.BranchCode,
            BookingDate: e.BookingDate,
            ValueDate: e.ValueDate,
            DaysVariance: e.DaysVariance,
            Amount: e.Amount.Amount,
            TransactionNature: e.TransactionNature.ToString(),
            Reason: e.Reason,
            RegulatoryReference: e.RegulatoryReference,
            MakerUserId: e.MakerUserId,
            Status: e.Status.ToString(),
            CheckerUserId: e.CheckerUserId,
            CheckerRemarks: e.CheckerRemarks,
            ActionedAtUtc: e.ActionedAtUtc,
            ExecutedVoucherNumber: e.ExecutedVoucherNumber,
            RetroactiveInterestCalculated: e.RetroactiveInterestCalculated,
            RetroactiveInterestVoucherNumber: e.RetroactiveInterestVoucherNumber,
            CreatedAtUtc: e.CreatedAtUtc);
}
