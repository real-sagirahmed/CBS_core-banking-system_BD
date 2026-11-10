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

public class SecurityAuditCommandHandler
{
    private readonly ISecurityAuditRepository _repository;
    private readonly IJwtTokenService? _jwtTokenService;

    public SecurityAuditCommandHandler(
        ISecurityAuditRepository repository,
        IJwtTokenService? jwtTokenService = null)
    {
        _repository = repository;
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
        string expectedPreviousHash = AuditJournalEntry.GenesisHash;

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
            null);
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
            items);
    }
}
