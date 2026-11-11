using CBS.Application.Reporting.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Reporting;

namespace CBS.Application.Reporting.Commands;

public class MisReportingCommandHandler
{
    private readonly IMisReportingRepository _repository;

    public MisReportingCommandHandler(IMisReportingRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<DailyCleanCashReportDto> GetDailyCleanCashReportAsync(
        string? branchCode,
        DateOnly date,
        CancellationToken ct = default)
    {
        return await _repository.GetDailyCleanCashReportAsync(branchCode, date, ct);
    }

    public async Task<DailyAffairsReportDto> GetDailyAffairsReportAsync(
        string? branchCode,
        DateOnly date,
        CancellationToken ct = default)
    {
        return await _repository.GetDailyAffairsReportAsync(branchCode, date, ct);
    }

    public async Task<DepositAnalyticsReportDto> GetDepositAnalyticsReportAsync(
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        return await _repository.GetDepositAnalyticsReportAsync(asOfDate, ct);
    }

    public async Task<ScanUnclaimedDepositsResponseDto> ScanAndRegisterUnclaimedDepositsAsync(
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        var candidates = await _repository.ScanUnclaimedDepositCandidatesAsync(asOfDate, ct);

        foreach (var c in candidates)
        {
            var existing = await _repository.GetUnclaimedDepositByAccountNumberAsync(c.AccountNumber, ct);
            if (existing is null)
            {
                var item = new UnclaimedDepositItem(
                    c.AccountNumber,
                    c.CustomerCif,
                    c.CustomerName,
                    c.AccountType,
                    c.Balance,
                    c.Currency,
                    c.LastTransactionDate,
                    asOfDate);

                await _repository.SaveUnclaimedDepositItemAsync(item, ct);
            }
        }

        decimal totalAmount = candidates.Sum(x => x.Balance);
        return new ScanUnclaimedDepositsResponseDto(candidates.Count, totalAmount, candidates);
    }

    public async Task IssueUnclaimedNoticeAsync(
        IssueNoticeRequestDto request,
        CancellationToken ct = default)
    {
        var item = await _repository.GetUnclaimedDepositByAccountNumberAsync(request.AccountNumber, ct)
            ?? throw new KeyNotFoundException($"Unclaimed deposit record for account '{request.AccountNumber}' not found.");

        item.IssueNotice(request.NoticeReference, request.NoticeDate);
        await _repository.SaveUnclaimedDepositItemAsync(item, ct);
    }

    public async Task<TransferUnclaimedDepositsResponseDto> TransferUnclaimedDepositsToCentralBankAsync(
        TransferUnclaimedDepositsRequestDto request,
        CancellationToken ct = default)
    {
        if (request.AccountNumbers == null || request.AccountNumbers.Count == 0)
            throw new ArgumentException("At least one account number must be specified for remittance.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.AuthorizedByUserId))
            throw new ArgumentException("Authorizing user ID is required.", nameof(request));

        decimal totalAmountTransferred = 0m;
        var transferredList = new List<string>();

        foreach (var accNo in request.AccountNumbers)
        {
            var item = await _repository.GetUnclaimedDepositByAccountNumberAsync(accNo, ct)
                ?? throw new KeyNotFoundException($"Unclaimed deposit record for account '{accNo}' not found.");

            item.RemitToCentralBank(request.TransferScheduleId, request.CentralBankReceiptRef, request.RemittanceDate);
            await _repository.SaveUnclaimedDepositItemAsync(item, ct);

            // Execute actual account balance debit and state update
            await _repository.ExecuteCentralBankFundTransferAsync(accNo, request.RemittanceDate, ct);

            totalAmountTransferred += item.CurrentBalance;
            transferredList.Add(accNo);
        }

        return new TransferUnclaimedDepositsResponseDto(
            request.TransferScheduleId,
            transferredList.Count,
            totalAmountTransferred,
            request.CentralBankReceiptRef,
            request.RemittanceDate,
            transferredList);
    }

    public async Task<Sbs3ClassificationReportDto> GetSbs3ClassificationReportAsync(
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        return await _repository.GetSbs3ClassificationReportAsync(asOfDate, ct);
    }

    public async Task<ForeignTradeSummaryReportDto> GetForeignTradeSummaryReportAsync(
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        return await _repository.GetForeignTradeSummaryReportAsync(asOfDate, ct);
    }

    public async Task<RemittanceIncentiveReportDto> GetRemittanceIncentiveReportAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        return await _repository.GetRemittanceIncentiveReportAsync(fromDate, toDate, ct);
    }

    public async Task<AdcReconciliationReportDto> GetAdcReconciliationReportAsync(
        DateOnly date,
        CancellationToken ct = default)
    {
        return await _repository.GetAdcReconciliationReportAsync(date, ct);
    }

    public async Task<PreservedReportSummaryDto> SealStatutoryReportAsync(
        SealReportRequestDto request,
        CancellationToken ct = default)
    {
        var archive = StatutoryReportArchive.Create(
            request.ReportType,
            request.ReportingCutoffDate,
            request.BranchId,
            request.GeneratedByUserId,
            request.ExportFormat,
            request.SerializedPayload,
            request.RecordCount);

        await _repository.SaveArchiveAsync(archive, ct);

        return new PreservedReportSummaryDto(
            archive.Id,
            archive.ReportType,
            archive.ReportingCutoffDate,
            archive.BranchId,
            archive.GenerationTimestamp,
            archive.GeneratedByUserId,
            archive.ExportFormat,
            archive.ContentHash,
            archive.RecordCount,
            archive.IsSubmitted,
            archive.SubmissionReference,
            archive.SubmissionTimestamp,
            archive.VerifyIntegrity());
    }

    public async Task SubmitStatutoryReportAsync(
        Guid id,
        SubmitReportRequestDto request,
        CancellationToken ct = default)
    {
        var archive = await _repository.GetArchiveByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Archived report '{id}' not found.");

        archive.MarkAsSubmitted(request.SubmissionReference, request.SubmittedByUserId);
        await _repository.SaveArchiveAsync(archive, ct);
    }

    public async Task<IReadOnlyList<PreservedReportSummaryDto>> GetArchivedReportsAsync(
        RegulatoryReportType? type,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct = default)
    {
        var archives = await _repository.GetArchivesAsync(type, fromDate, toDate, ct);
        return archives.Select(a => new PreservedReportSummaryDto(
            a.Id,
            a.ReportType,
            a.ReportingCutoffDate,
            a.BranchId,
            a.GenerationTimestamp,
            a.GeneratedByUserId,
            a.ExportFormat,
            a.ContentHash,
            a.RecordCount,
            a.IsSubmitted,
            a.SubmissionReference,
            a.SubmissionTimestamp,
            a.VerifyIntegrity())).ToList();
    }

    public async Task<(PreservedReportSummaryDto Summary, string SerializedPayload)> GetArchivedReportDetailAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var archive = await _repository.GetArchiveByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Archived report '{id}' not found.");

        var summary = new PreservedReportSummaryDto(
            archive.Id,
            archive.ReportType,
            archive.ReportingCutoffDate,
            archive.BranchId,
            archive.GenerationTimestamp,
            archive.GeneratedByUserId,
            archive.ExportFormat,
            archive.ContentHash,
            archive.RecordCount,
            archive.IsSubmitted,
            archive.SubmissionReference,
            archive.SubmissionTimestamp,
            archive.VerifyIntegrity());

        return (summary, archive.SerializedPayload);
    }
}
