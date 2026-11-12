using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Application.Governance.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Reporting;
using CBS.Domain.Security;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Governance.Services;

// ARCHITECTURAL INTENT: Unified CBS Governance, Break-Glass Emergency Security & Daily Proofing Service
// (BRPD Circular 12/2024 Modules 9, 10 & 11).
// Orchestrates continuous transaction anomaly screening, time-bounded emergency PAM sessions with dual custody,
// and day-end multi-rail general ledger & cash proofing before book closure.

public sealed class GovernanceAndProofingService : IGovernanceAndProofingService
{
    private readonly ITransactionAnomalyRepository _anomalyRepository;
    private readonly IBreakGlassSessionRepository _breakGlassRepository;
    private readonly IDailyEodBalancingRepository _eodBalancingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public GovernanceAndProofingService(
        ITransactionAnomalyRepository anomalyRepository,
        IBreakGlassSessionRepository breakGlassRepository,
        IDailyEodBalancingRepository eodBalancingRepository,
        IUnitOfWork unitOfWork)
    {
        _anomalyRepository = anomalyRepository ?? throw new ArgumentNullException(nameof(anomalyRepository));
        _breakGlassRepository = breakGlassRepository ?? throw new ArgumentNullException(nameof(breakGlassRepository));
        _eodBalancingRepository = eodBalancingRepository ?? throw new ArgumentNullException(nameof(eodBalancingRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    // =========================================================================
    // 1. Continuous Transaction Anomaly Detection
    // =========================================================================

    public async Task<TransactionAnomalyResponse?> ScreenAndDetectAnomalyAsync(
        DetectAnomalyRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        DateTime txTime = request.TransactionTimeUtc ?? DateTime.UtcNow;

        // Rule A: Employee Self-Account Posting Check (Throws InvariantViolationException if identical)
        if (!string.IsNullOrWhiteSpace(request.OriginatingTellerId) && !string.IsNullOrWhiteSpace(request.AccountOwnerEmployeeId))
        {
            if (request.OriginatingTellerId.Equals(request.AccountOwnerEmployeeId, StringComparison.OrdinalIgnoreCase))
            {
                // This triggers the EmployeeSelfAccountPostingInvariant directly
                TransactionAnomalyRecord.CreateEmployeeSelfAccountAnomaly(
                    request.OriginatingTellerId,
                    request.AccountOwnerEmployeeId,
                    request.AccountNumber,
                    request.CifNumber,
                    request.TransactionReference,
                    Money.Bdt(request.CurrentAmount),
                    txTime);
            }
        }

        // Rule B: Structuring / Smurfing Detection (Cumulative 24h total >= 1,000,000 BDT)
        if (request.Cumulative24hAmount >= 1_000_000m && request.CurrentAmount < 1_000_000m)
        {
            var anomaly = TransactionAnomalyRecord.DetectStructuring(
                request.AccountNumber,
                request.CifNumber,
                request.TransactionReference,
                Money.Bdt(request.CurrentAmount),
                Money.Bdt(request.Cumulative24hAmount),
                txTime);

            await _anomalyRepository.AddAsync(anomaly, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return MapAnomalyToResponse(anomaly);
        }

        // Rule C: Off-Hours Posting Detection (Outside 09:00 - 18:00 UTC)
        int hour = txTime.Hour;
        if (hour < 4 || hour > 15) // Approx Bangladesh Bank standard local operating window mapped to UTC
        {
            var anomaly = TransactionAnomalyRecord.DetectOffHoursPosting(
                request.AccountNumber,
                request.CifNumber,
                request.TransactionReference,
                Money.Bdt(request.CurrentAmount),
                request.OriginatingTellerId ?? "UNKNOWN",
                txTime);

            await _anomalyRepository.AddAsync(anomaly, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return MapAnomalyToResponse(anomaly);
        }

        return null;
    }

    public async Task<TransactionAnomalyResponse> ResolveAnomalyAsync(
        string anomalyId,
        ResolveAnomalyRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var anomaly = await _anomalyRepository.GetByIdAsync(anomalyId, ct)
            ?? throw new KeyNotFoundException($"Transaction anomaly record '{anomalyId}' was not found.");

        anomaly.Resolve(request.Resolution, request.InvestigatedByUserId, request.Notes);

        await _anomalyRepository.UpdateAsync(anomaly, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapAnomalyToResponse(anomaly);
    }

    public async Task<IReadOnlyList<TransactionAnomalyResponse>> GetPendingAnomaliesAsync(CancellationToken ct = default)
    {
        var records = await _anomalyRepository.GetPendingAnomaliesAsync(ct);
        return records.Select(MapAnomalyToResponse).ToList();
    }

    private static TransactionAnomalyResponse MapAnomalyToResponse(TransactionAnomalyRecord a)
    {
        return new TransactionAnomalyResponse(
            a.AnomalyId,
            a.AccountNumber,
            a.CifNumber,
            a.TransactionReference,
            a.AnomalyType.ToString(),
            a.TransactionAmount.Amount,
            a.RuleDescription,
            a.DetectedAtUtc,
            a.Status.ToString(),
            a.InvestigatedByUserId,
            a.InvestigationNotes,
            a.ResolvedAtUtc);
    }

    // =========================================================================
    // 2. Break-Glass Emergency Privileged Sessions
    // =========================================================================

    public async Task<BreakGlassSessionResponse> RequestBreakGlassSessionAsync(
        RequestBreakGlassSessionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        string sessionId = $"BG-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        var session = new BreakGlassEmergencySession(
            sessionId,
            request.RequestorUserId,
            request.TargetRole,
            request.EmergencyJustification,
            DateTime.UtcNow);

        await _breakGlassRepository.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapBreakGlassToResponse(session);
    }

    public async Task<BreakGlassSessionResponse> ApproveBreakGlassSessionAsync(
        string sessionId,
        ApproveBreakGlassSessionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var session = await _breakGlassRepository.GetByIdAsync(sessionId, ct)
            ?? throw new KeyNotFoundException($"Break-glass emergency session '{sessionId}' was not found.");

        if (session.Status == BreakGlassStatus.RequestedPendingApproval)
        {
            session.ApproveFirstOfficer(request.ApproverUserId);
        }
        else if (session.Status == BreakGlassStatus.PartiallyApprovedFirstOfficer)
        {
            session.ApproveSecondOfficer(request.ApproverUserId);
        }
        else
        {
            throw new InvalidOperationException($"Cannot approve session in status '{session.Status}'.");
        }

        await _breakGlassRepository.UpdateAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapBreakGlassToResponse(session);
    }

    public async Task<BreakGlassSessionResponse> RevokeBreakGlassSessionAsync(
        string sessionId,
        RevokeBreakGlassSessionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var session = await _breakGlassRepository.GetByIdAsync(sessionId, ct)
            ?? throw new KeyNotFoundException($"Break-glass emergency session '{sessionId}' was not found.");

        session.Revoke(request.RevokedByUserId, request.Reason);

        await _breakGlassRepository.UpdateAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapBreakGlassToResponse(session);
    }

    public async Task<BreakGlassSessionResponse> GetBreakGlassSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var session = await _breakGlassRepository.GetByIdAsync(sessionId, ct)
            ?? throw new KeyNotFoundException($"Break-glass emergency session '{sessionId}' was not found.");

        return MapBreakGlassToResponse(session);
    }

    public async Task<bool> ValidateActiveBreakGlassSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var session = await _breakGlassRepository.GetByIdAsync(sessionId, ct)
            ?? throw new KeyNotFoundException($"Break-glass emergency session '{sessionId}' was not found.");

        session.AssertSessionValid(DateTime.UtcNow);
        return true;
    }

    private static BreakGlassSessionResponse MapBreakGlassToResponse(BreakGlassEmergencySession s)
    {
        return new BreakGlassSessionResponse(
            s.SessionId,
            s.RequestorUserId,
            s.TargetRole,
            s.EmergencyJustification,
            s.FirstApproverUserId,
            s.FirstApprovedAtUtc,
            s.SecondApproverUserId,
            s.SecondApprovedAtUtc,
            s.SessionExpiresAtUtc,
            s.Status.ToString(),
            s.RevokedByUserId,
            s.RevocationReason,
            s.RevokedAtUtc,
            s.CreatedAtUtc);
    }

    // =========================================================================
    // 3. Consolidated Daily EOD Balancing & Proofing
    // =========================================================================

    public async Task<DailyEodBalancingSheetResponse> GenerateDailyBalancingSheetAsync(
        GenerateDailyBalancingSheetRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        string sheetId = $"EOD-BAL-{request.BranchCode}-{request.OperatingDate:yyyyMMdd}";
        var existing = await _eodBalancingRepository.GetByIdAsync(sheetId, ct);
        if (existing != null)
        {
            return MapBalancingSheetToResponse(existing);
        }

        var sheet = new DailyEodBalancingSheet(
            sheetId,
            request.BranchCode,
            request.OperatingDate,
            Money.Bdt(request.TotalGlDebits),
            Money.Bdt(request.TotalGlCredits),
            Money.Bdt(request.TotalVaultCashBalance),
            Money.Bdt(request.TotalTellerCashBalance),
            Money.Bdt(request.TotalClearingSettlementDebits),
            Money.Bdt(request.TotalClearingSettlementCredits),
            Money.Bdt(request.ClearingSuspenseBalance),
            DateTime.UtcNow);

        await _eodBalancingRepository.AddAsync(sheet, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapBalancingSheetToResponse(sheet);
    }

    public async Task<DailyEodBalancingSheetResponse> ProofDailyBooksAsync(
        string sheetId,
        ProofDailyBooksRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var sheet = await _eodBalancingRepository.GetByIdAsync(sheetId, ct)
            ?? throw new KeyNotFoundException($"Daily balancing sheet '{sheetId}' was not found.");

        sheet.ValidateAndProofDailyBooks(request.ManagerUserId);

        await _eodBalancingRepository.UpdateAsync(sheet, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapBalancingSheetToResponse(sheet);
    }

    public async Task<DailyEodBalancingSheetResponse> GetDailyBalancingSheetAsync(string sheetId, CancellationToken ct = default)
    {
        var sheet = await _eodBalancingRepository.GetByIdAsync(sheetId, ct)
            ?? throw new KeyNotFoundException($"Daily balancing sheet '{sheetId}' was not found.");

        return MapBalancingSheetToResponse(sheet);
    }

    private static DailyEodBalancingSheetResponse MapBalancingSheetToResponse(DailyEodBalancingSheet s)
    {
        return new DailyEodBalancingSheetResponse(
            s.SheetId,
            s.BranchCode,
            s.OperatingDate,
            s.TotalGlDebits.Amount,
            s.TotalGlCredits.Amount,
            s.TotalVaultCashBalance.Amount,
            s.TotalTellerCashBalance.Amount,
            s.TotalClearingSettlementDebits.Amount,
            s.TotalClearingSettlementCredits.Amount,
            s.ClearingSuspenseBalance.Amount,
            s.ProofingStatus.ToString(),
            s.DiscrepancyNotes,
            s.ApprovedByManagerUserId,
            s.ProofedAtUtc,
            s.CreatedAtUtc);
    }
}
