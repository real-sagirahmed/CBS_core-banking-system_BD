// ARCHITECTURAL INTENT: Enterprise System Administration Service orchestrating Single-Session Enforcement,
// Admin Maker-Checker Dual-Control, Bank Global Parameters, and SoD Financial Restrictions
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10 (10.3.5-vii, viii, x; 10.3.2).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Application.Security.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Security;

namespace CBS.Application.Security.Services;

public class EnterpriseAdministrationService : IEnterpriseAdministrationService
{
    private readonly IUserActiveSessionRepository _sessionRepository;
    private readonly IAdminChangeRequestRepository _adminChangeRepository;
    private readonly IBankGlobalParametersRepository _parametersRepository;
    private readonly IUnitOfWork _unitOfWork;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EnterpriseAdministrationService(
        IUserActiveSessionRepository sessionRepository,
        IAdminChangeRequestRepository adminChangeRepository,
        IBankGlobalParametersRepository parametersRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository;
        _adminChangeRepository = adminChangeRepository;
        _parametersRepository = parametersRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UserActiveSessionResponseDto> AuthenticateSessionAsync(LoginWithSessionControlRequest request, CancellationToken ct = default)
    {
        var existing = await _sessionRepository.GetActiveSessionByUserIdAsync(request.UserId, ct);
        if (existing != null && existing.IsActive)
        {
            if (request.Policy == ConcurrentSessionPolicy.BlockNewSession)
            {
                throw new InvariantViolationException(
                    "ConcurrentSessionDisallowedInvariant",
                    "BRPD Circular 12/2024 (10.3.5-viii)",
                    $"User '{request.UserId}' already has an active session (Session ID: '{existing.SessionId}') on terminal '{existing.TerminalIp}'. Multiple concurrent sessions are prohibited.");
            }

            // TerminatePriorSession policy: Invalidate prior session with audit trail
            existing.TerminateSession(
                SessionTerminationReason.SupersededByNewLogin,
                $"Superseded by subsequent login from terminal '{request.TerminalIp}' at {DateTime.UtcNow:O}");
            await _sessionRepository.UpdateAsync(existing, ct);
        }

        var sessionId = $"SESS-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var session = new UserActiveSession(
            sessionId: sessionId,
            userId: request.UserId,
            terminalIp: request.TerminalIp,
            appServerIp: request.AppServerIp,
            branchCode: request.BranchCode);

        await _sessionRepository.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapSessionDto(session);
    }

    public async Task<UserActiveSessionResponseDto> TerminateSessionAsync(string sessionId, TerminateSessionRequest request, CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetBySessionIdAsync(sessionId, ct)
            ?? throw new KeyNotFoundException($"Active session '{sessionId}' was not found.");

        session.TerminateSession(request.Reason, request.Notes ?? $"Terminated by user '{request.TerminatedByUserId}'");
        await _sessionRepository.UpdateAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapSessionDto(session);
    }

    public async Task<IReadOnlyList<UserActiveSessionResponseDto>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        var sessions = await _sessionRepository.GetActiveSessionsAsync(ct);
        return sessions.Select(MapSessionDto).ToList();
    }

    public async Task<AdminChangeRequestResponseDto> SubmitAdminChangeRequestAsync(CreateAdminChangeRequestDto request, CancellationToken ct = default)
    {
        var requestId = $"ADM-REQ-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var change = new AdminChangeRequest(
            requestId: requestId,
            category: request.Category,
            changeDescription: request.ChangeDescription,
            changePayloadJson: request.ChangePayloadJson,
            makerUserId: request.MakerUserId,
            makerBranchCode: request.MakerBranchCode);

        await _adminChangeRepository.AddAsync(change, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapAdminChangeDto(change);
    }

    public async Task<AdminChangeRequestResponseDto> ApproveAdminChangeRequestAsync(string requestId, ApproveAdminChangeRequestDto request, CancellationToken ct = default)
    {
        var change = await _adminChangeRepository.GetByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException($"Administrative change request '{requestId}' was not found.");

        change.Approve(request.CheckerUserId, request.Remarks);

        // If this change was for BankGlobalParameters, automatically commit parameter changes to database
        if (change.Category == AdminChangeCategory.BankGlobalParameters && !string.IsNullOrWhiteSpace(change.ChangePayloadJson))
        {
            try
            {
                var paramUpdate = JsonSerializer.Deserialize<UpdateBankGlobalParametersRequest>(change.ChangePayloadJson, JsonOptions);
                if (paramUpdate != null)
                {
                    await UpdateBankParametersInternalAsync(paramUpdate, ct);
                }
            }
            catch (Exception ex) when (ex is not InvariantViolationException)
            {
                // Payload deserialization error fallback
            }
        }

        await _adminChangeRepository.UpdateAsync(change, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapAdminChangeDto(change);
    }

    public async Task<AdminChangeRequestResponseDto> RejectAdminChangeRequestAsync(string requestId, RejectAdminChangeRequestDto request, CancellationToken ct = default)
    {
        var change = await _adminChangeRepository.GetByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException($"Administrative change request '{requestId}' was not found.");

        change.Reject(request.CheckerUserId, request.RejectionReason);
        await _adminChangeRepository.UpdateAsync(change, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapAdminChangeDto(change);
    }

    public async Task<IReadOnlyList<AdminChangeRequestResponseDto>> GetPendingAdminChangeRequestsAsync(CancellationToken ct = default)
    {
        var requests = await _adminChangeRepository.GetPendingRequestsAsync(ct);
        return requests.Select(MapAdminChangeDto).ToList();
    }

    public async Task<BankGlobalParametersResponseDto> GetBankParametersAsync(CancellationToken ct = default)
    {
        var parameters = await _parametersRepository.GetParametersAsync("BD-CBS-001", ct);
        if (parameters == null)
        {
            parameters = BankGlobalParameters.CreateDefault("SYSTEM");
            await _parametersRepository.SaveParametersAsync(parameters, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return MapParametersDto(parameters);
    }

    public async Task<BankGlobalParametersResponseDto> UpdateBankParametersDirectAsync(UpdateBankGlobalParametersRequest request, CancellationToken ct = default)
    {
        var parameters = await UpdateBankParametersInternalAsync(request, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapParametersDto(parameters);
    }

    private async Task<BankGlobalParameters> UpdateBankParametersInternalAsync(UpdateBankGlobalParametersRequest request, CancellationToken ct)
    {
        var parameters = await _parametersRepository.GetParametersAsync("BD-CBS-001", ct)
            ?? BankGlobalParameters.CreateDefault(request.UpdatedByUserId);

        parameters.UpdateParameters(
            bankName: request.BankName,
            baseCurrencyCode: request.BaseCurrencyCode,
            localCurrencyCode: request.LocalCurrencyCode,
            chiefLocalOfficeBranchCode: request.ChiefLocalOfficeBranchCode,
            weeklyHolidaysCsv: request.WeeklyHolidaysCsv,
            fiscalYearStartMonth: request.FiscalYearStartMonth,
            fiscalYearStartDay: request.FiscalYearStartDay,
            realTimeBranchSettlementEnabled: request.RealTimeBranchSettlementEnabled,
            allowAlphaCustomerNumber: request.AllowAlphaCustomerNumber,
            maxExternalAccountNumberLength: request.MaxExternalAccountNumberLength,
            dailySpotRateListDetailsJson: request.DailySpotRateListDetailsJson,
            updatedByUserId: request.UpdatedByUserId);

        await _parametersRepository.SaveParametersAsync(parameters, ct);
        return parameters;
    }

    public void ValidateFinancialOperationPermission(string? userRole, string operationName)
    {
        AdminFinancialRestrictionPolicy.EnsureNonAdminFinancialRole(userRole, operationName);
    }

    private static UserActiveSessionResponseDto MapSessionDto(UserActiveSession s) =>
        new(
            SessionId: s.SessionId,
            UserId: s.UserId,
            TerminalIp: s.TerminalIp,
            AppServerIp: s.AppServerIp,
            BranchCode: s.BranchCode,
            LoginTimeUtc: s.LoginTimeUtc,
            LastActivityUtc: s.LastActivityUtc,
            IsActive: s.IsActive,
            TerminationReason: s.TerminationReason?.ToString(),
            TerminationNotes: s.TerminationNotes,
            TerminatedAtUtc: s.TerminatedAtUtc);

    private static AdminChangeRequestResponseDto MapAdminChangeDto(AdminChangeRequest c) =>
        new(
            RequestId: c.RequestId,
            Category: c.Category.ToString(),
            ChangeDescription: c.ChangeDescription,
            ChangePayloadJson: c.ChangePayloadJson,
            MakerUserId: c.MakerUserId,
            MakerBranchCode: c.MakerBranchCode,
            Status: c.Status.ToString(),
            CreatedAtUtc: c.CreatedAtUtc,
            CheckerUserId: c.CheckerUserId,
            CheckerRemarks: c.CheckerRemarks,
            ActionedAtUtc: c.ActionedAtUtc,
            RejectionReason: c.RejectionReason);

    private static BankGlobalParametersResponseDto MapParametersDto(BankGlobalParameters p) =>
        new(
            BankCode: p.BankCode,
            BankName: p.BankName,
            BaseCurrencyCode: p.BaseCurrencyCode,
            LocalCurrencyCode: p.LocalCurrencyCode,
            ChiefLocalOfficeBranchCode: p.ChiefLocalOfficeBranchCode,
            WeeklyHolidaysCsv: p.WeeklyHolidaysCsv,
            FiscalYearStartMonth: p.FiscalYearStartMonth,
            FiscalYearStartDay: p.FiscalYearStartDay,
            RealTimeBranchSettlementEnabled: p.RealTimeBranchSettlementEnabled,
            AllowAlphaCustomerNumber: p.AllowAlphaCustomerNumber,
            MaxExternalAccountNumberLength: p.MaxExternalAccountNumberLength,
            DailySpotRateListDetailsJson: p.DailySpotRateListDetailsJson,
            LastUpdatedAtUtc: p.LastUpdatedAtUtc,
            LastUpdatedByUserId: p.LastUpdatedByUserId);
}
