using CBS.Application.Common.Interfaces;
using CBS.Application.Common.Security;
using CBS.Application.SecurityAudit.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.SecurityAudit;
using CBS.Domain.ValueObjects;

namespace CBS.Application.SecurityAudit.Commands;

public sealed record LoginSessionCommand(LoginSessionRequest Request);
public sealed record LogoutSessionCommand(LogoutSessionRequest Request);
public sealed record AssertFinancialPermissionCommand(AssertFinancialPermissionRequest Request);
public sealed record LogAuditEntryCommand(LogAuditEntryRequest Request);
public sealed record GetCaatsAuditExtractCommand(DateTime? FromUtc = null, DateTime? ToUtc = null);
public sealed record GenerateCtrReportCommand(GenerateCtrReportRequest Request);
public sealed record EvaluateStrAlertsCommand(EvaluateStrAlertsRequest Request);
public sealed record AutoAggregateCtrCommand(AutoAggregateCtrRequest Request);
public sealed record CaatsVerifyIntegrityCommand();
public sealed record ExportGoAmlReportCommand(string ReportReference, string Format = "xml");

public class SecurityAuditCommandHandler
{
    private readonly ISecurityAuditRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFinancialVoucherRepository? _voucherRepository;
    private readonly IJwtTokenService? _jwtTokenService;

    public SecurityAuditCommandHandler(
        ISecurityAuditRepository repository,
        IUnitOfWork unitOfWork,
        IFinancialVoucherRepository? voucherRepository = null,
        IJwtTokenService? jwtTokenService = null)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _voucherRepository = voucherRepository;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<UserSessionResponse> HandleLoginAsync(LoginSessionCommand command, CancellationToken ct = default)
    {
        var req = command.Request;

        // BRPD 10.3.5-viii: Check if user already has an active session
        var existingActive = await _repository.GetActiveSessionByUserIdAsync(req.UserId, ct);
        bool hasExisting = existingActive is not null && existingActive.IsActive;

        var session = UserSession.StartSession(
            req.SessionId,
            req.UserId,
            req.Role,
            req.BranchCode,
            req.TerminalIp,
            hasExisting);

        await _repository.AddSessionAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        string? token = _jwtTokenService?.GenerateToken(session.UserId, session.Role, session.BranchCode, session.SessionId);
        DateTime? expiresAtUtc = token is not null ? DateTime.UtcNow.AddMinutes(15) : null;

        return new UserSessionResponse(
            session.SessionId,
            session.UserId,
            session.Role.ToString(),
            session.BranchCode,
            session.TerminalIp,
            session.LoggedInAtUtc,
            session.IsActive,
            token,
            expiresAtUtc);
    }

    public async Task<UserSessionResponse> HandleLogoutAsync(LogoutSessionCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var session = await _repository.GetSessionByIdAsync(req.SessionId, ct)
            ?? throw new KeyNotFoundException($"Session '{req.SessionId}' not found.");

        session.TerminateSession();
        await _repository.UpdateSessionAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new UserSessionResponse(
            session.SessionId,
            session.UserId,
            session.Role.ToString(),
            session.BranchCode,
            session.TerminalIp,
            session.LoggedInAtUtc,
            session.IsActive);
    }

    public async Task HandleAssertFinancialPermissionAsync(AssertFinancialPermissionCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var session = await _repository.GetSessionByIdAsync(req.SessionId, ct)
            ?? throw new KeyNotFoundException($"Session '{req.SessionId}' not found.");

        session.AssertCanPerformFinancialTransaction();
    }

    public async Task<AuditJournalResponse> HandleLogAuditAsync(LogAuditEntryCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var latestSeq = await _repository.GetLatestAuditSequenceAsync(ct);
        var latestHash = await _repository.GetLatestAuditHashAsync(ct);

        var entry = AuditJournalEntry.Create(
            sequenceNumber: latestSeq + 1,
            userId: req.UserId,
            role: req.Role,
            branchCode: req.BranchCode,
            eventCategory: req.EventCategory,
            action: req.Action,
            targetEntityName: req.TargetEntityName,
            targetEntityId: req.TargetEntityId,
            beforeStateJson: req.BeforeStateJson,
            afterStateJson: req.AfterStateJson,
            terminalIp: req.TerminalIp,
            previousEntryHash: latestHash);

        await _repository.AddAuditEntryAsync(entry, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new AuditJournalResponse(
            entry.SequenceNumber,
            entry.TimestampUtc,
            entry.UserId,
            entry.Role.ToString(),
            entry.BranchCode,
            entry.EventCategory.ToString(),
            entry.Action,
            entry.TargetEntityName,
            entry.TargetEntityId,
            entry.PreviousEntryHash,
            entry.CurrentEntryHash,
            entry.VerifyHash());
    }

    public async Task<CaatsAuditExtractResponse> HandleCaatsExtractAsync(GetCaatsAuditExtractCommand command, CancellationToken ct = default)
    {
        var entries = await _repository.GetAuditEntriesAsync(command.FromUtc, command.ToUtc, ct);

        // Verify cryptographic hash chain integrity
        bool isChainValid = true;
        string expectedPreviousHash = entries.Count > 0 ? entries[0].PreviousEntryHash : AuditJournalEntry.GenesisHash;

        var mappedList = new List<AuditJournalResponse>();

        foreach (var e in entries)
        {
            bool validHash = e.VerifyHash();
            bool validLink = string.Equals(e.PreviousEntryHash, expectedPreviousHash, StringComparison.OrdinalIgnoreCase);

            if (!validHash || !validLink)
            {
                isChainValid = false;
            }

            expectedPreviousHash = e.CurrentEntryHash;

            mappedList.Add(new AuditJournalResponse(
                e.SequenceNumber,
                e.TimestampUtc,
                e.UserId,
                e.Role.ToString(),
                e.BranchCode,
                e.EventCategory.ToString(),
                e.Action,
                e.TargetEntityName,
                e.TargetEntityId,
                e.PreviousEntryHash,
                e.CurrentEntryHash,
                validHash && validLink));
        }

        return new CaatsAuditExtractResponse(mappedList.Count, isChainValid, mappedList.AsReadOnly());
    }

    public async Task<CaatsIntegrityVerificationResultDto> HandleCaatsVerifyIntegrityAsync(CaatsVerifyIntegrityCommand command, CancellationToken ct = default)
    {
        var entries = await _repository.GetAuditEntriesAsync(null, null, ct);
        var ordered = entries.OrderBy(e => e.SequenceNumber).ToList();

        string expectedPreviousHash = AuditJournalEntry.GenesisHash;
        string lastVerifiedHash = expectedPreviousHash;

        foreach (var entry in ordered)
        {
            // 1. Verify SHA-256 integrity of current entry's state
            if (!entry.VerifyHash())
            {
                return new CaatsIntegrityVerificationResultDto(
                    TotalRecordsEvaluated: ordered.Count,
                    IsChainIntegrityValid: false,
                    TamperDetectedAtSequence: entry.SequenceNumber,
                    LastVerifiedHash: lastVerifiedHash,
                    Remarks: $"Cryptographic tamper detected at sequence #{entry.SequenceNumber}. Stored hash '{entry.CurrentEntryHash}' does not match recomputed SHA-256 hash.");
            }

            // 2. Verify link with previous entry
            if (!string.Equals(entry.PreviousEntryHash, expectedPreviousHash, StringComparison.OrdinalIgnoreCase))
            {
                return new CaatsIntegrityVerificationResultDto(
                    TotalRecordsEvaluated: ordered.Count,
                    IsChainIntegrityValid: false,
                    TamperDetectedAtSequence: entry.SequenceNumber,
                    LastVerifiedHash: lastVerifiedHash,
                    Remarks: $"Cryptographic chain linkage broken at sequence #{entry.SequenceNumber}. Expected previous hash '{expectedPreviousHash}' but found '{entry.PreviousEntryHash}'.");
            }

            expectedPreviousHash = entry.CurrentEntryHash;
            lastVerifiedHash = entry.CurrentEntryHash;
        }

        return new CaatsIntegrityVerificationResultDto(
            TotalRecordsEvaluated: ordered.Count,
            IsChainIntegrityValid: true,
            TamperDetectedAtSequence: null,
            LastVerifiedHash: lastVerifiedHash,
            Remarks: "Audit trail cryptographic integrity verified 100%. All SHA-256 hashes and sequence links are valid.");
    }

    public async Task<BfiuReportResponse> HandleGenerateCtrAsync(GenerateCtrReportCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var aggregates = req.Aggregates.Select(a => (a.CifNumber, a.CustomerName, a.TransactionCount, a.TotalCashAmount));

        var report = BfiuRegulatoryReport.GenerateCtr(
            req.ReportReference,
            req.ReportDate,
            aggregates,
            req.GeneratedByUserId);

        await _repository.AddBfiuReportAsync(report, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var items = report.CtrItems.Select(c => new CtrItemResponse(
            c.CifNumber,
            c.CustomerName,
            c.TransactionCount,
            c.TotalCashAmount,
            c.Currency,
            c.ReportDate)).ToList();

        return new BfiuReportResponse(
            report.ReportReference,
            report.ReportType,
            report.ReportDate,
            report.Status.ToString(),
            items.Count,
            items,
            null,
            report.BranchCode,
            report.GoAmlFormat,
            report.GoAmlPayload);
    }

    public async Task<BfiuReportResponse> HandleAutoAggregateCtrAsync(AutoAggregateCtrCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        string refNo = string.IsNullOrWhiteSpace(req.ReportReference)
            ? $"CTR-{req.ReportDate:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"
            : req.ReportReference.Trim();

        var aggregateList = new List<(string Cif, string Name, int TxCount, decimal TotalCash)>();

        if (req.Aggregates != null && req.Aggregates.Count > 0)
        {
            aggregateList.AddRange(req.Aggregates.Select(a => (a.CifNumber, a.CustomerName, a.TransactionCount, a.TotalCashAmount)));
        }
        else if (_voucherRepository != null)
        {
            var dateOnly = DateOnly.FromDateTime(req.ReportDate);
            var vouchers = await _voucherRepository.GetByDateRangeAsync(dateOnly, dateOnly, req.BranchCode, ct);
            var cashVouchers = vouchers
                .Where(v => v.Type == VoucherType.Cash && v.Status == VoucherStatus.Posted)
                .ToList();

            var grouped = cashVouchers
                .SelectMany(v => v.Entries)
                .GroupBy(e => e.AccountCode)
                .Select(g => (
                    Cif: g.Key,
                    Name: g.First().Narration,
                    TxCount: g.Count(),
                    TotalCash: g.Sum(e => e.BaseAmount.Amount)
                ));

            aggregateList.AddRange(grouped);
        }

        var report = BfiuRegulatoryReport.GenerateCtr(
            refNo,
            req.ReportDate,
            aggregateList,
            req.GeneratedByUserId,
            req.BranchCode);

        await _repository.AddBfiuReportAsync(report, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var items = report.CtrItems.Select(c => new CtrItemResponse(
            c.CifNumber,
            c.CustomerName,
            c.TransactionCount,
            c.TotalCashAmount,
            c.Currency,
            c.ReportDate)).ToList();

        return new BfiuReportResponse(
            report.ReportReference,
            report.ReportType,
            report.ReportDate,
            report.Status.ToString(),
            items.Count,
            items,
            null,
            report.BranchCode,
            report.GoAmlFormat,
            report.GoAmlPayload);
    }

    public async Task<BfiuReportResponse> HandleEvaluateStrAsync(EvaluateStrAlertsCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var alerts = req.Alerts.Select(a => new StrAlertItem(
            a.CifNumber,
            a.CustomerName,
            a.Reason,
            a.TransactionAmount,
            a.Currency,
            a.Narrative,
            DateTime.UtcNow));

        var report = BfiuRegulatoryReport.GenerateStr(
            req.ReportReference,
            req.ReportDate,
            alerts,
            req.GeneratedByUserId);

        await _repository.AddBfiuReportAsync(report, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var items = report.StrAlerts.Select(s => new StrItemResponse(
            s.CifNumber,
            s.CustomerName,
            s.Reason.ToString(),
            s.TransactionAmount,
            s.Currency,
            s.Narrative,
            s.DetectedAtUtc)).ToList();

        return new BfiuReportResponse(
            report.ReportReference,
            report.ReportType,
            report.ReportDate,
            report.Status.ToString(),
            items.Count,
            null,
            items,
            report.BranchCode,
            report.GoAmlFormat,
            report.GoAmlPayload);
    }

    public async Task<GoAmlExportResponseDto> HandleGoAmlExportAsync(ExportGoAmlReportCommand command, CancellationToken ct = default)
    {
        var report = await _repository.GetBfiuReportByReferenceAsync(command.ReportReference, ct)
            ?? throw new KeyNotFoundException($"BFIU regulatory report with reference '{command.ReportReference}' was not found.");

        string format = string.IsNullOrWhiteSpace(command.Format) ? "xml" : command.Format.Trim().ToLowerInvariant();
        string payload;

        if (format == "xml")
        {
            payload = GoAmlXmlBuilder.BuildXml(report);
        }
        else if (format == "json")
        {
            payload = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        else
        {
            throw new ArgumentException($"Unsupported goAML export format '{command.Format}'. Supported formats are 'xml' and 'json'.", nameof(command.Format));
        }

        report.SetGoAmlExportPayload(payload, format);
        await _unitOfWork.SaveChangesAsync(ct);

        return new GoAmlExportResponseDto(
            report.ReportReference,
            report.ReportType,
            format,
            payload,
            DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<BfiuReportResponse>> HandleGetAllBfiuReportsAsync(CancellationToken ct = default)
    {
        var reports = await _repository.GetAllBfiuReportsAsync(ct);
        return reports.Select(r => new BfiuReportResponse(
            r.ReportReference,
            r.ReportType,
            r.ReportDate,
            r.Status.ToString(),
            r.ReportType == "CTR" ? r.CtrItems.Count : r.StrAlerts.Count,
            r.CtrItems.Select(c => new CtrItemResponse(c.CifNumber, c.CustomerName, c.TransactionCount, c.TotalCashAmount, c.Currency, c.ReportDate)).ToList(),
            r.StrAlerts.Select(s => new StrItemResponse(s.CifNumber, s.CustomerName, s.Reason.ToString(), s.TransactionAmount, s.Currency, s.Narrative, s.DetectedAtUtc)).ToList(),
            r.BranchCode,
            r.GoAmlFormat,
            r.GoAmlPayload
        )).ToList();
    }
}
