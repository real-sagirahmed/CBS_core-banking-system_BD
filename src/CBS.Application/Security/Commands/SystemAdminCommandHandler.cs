using System.Text.Json;
using CBS.Application.Common.Interfaces;
using CBS.Application.Security.DTOs;
using CBS.Domain.Exceptions;
using CBS.Domain.Enums;
using CBS.Domain.Security;

namespace CBS.Application.Security.Commands;

public class SystemAdminCommandHandler
{
    private readonly ISystemAdminRepository _adminRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SystemAdminCommandHandler(
        ISystemAdminRepository adminRepository,
        IUnitOfWork unitOfWork)
    {
        _adminRepository = adminRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminProposalResponse> ProposeCreateUserAsync(ProposeCreateUserRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var existingUser = await _adminRepository.GetUserByIdAsync(request.UserId, ct);
        if (existingUser is not null)
        {
            throw new InvariantViolationException(
                "DuplicateUserIdInvariant",
                "BRPD Circular 12/2024 (10.1.4)",
                $"User with ID '{request.UserId}' already exists.");
        }

        string payloadJson = JsonSerializer.Serialize(request, JsonOptions);
        var proposal = new AdminActionProposal(
            Guid.NewGuid(),
            AdminActionType.CreateUser,
            request.UserId,
            payloadJson,
            request.MakerUserId,
            DateTime.UtcNow);

        await _adminRepository.AddProposalAsync(proposal, ct);

        var journal = new AdminAuditJournal(
            Guid.NewGuid(),
            JournalCategory.UserProfileActivity,
            "ProposeCreateUser",
            request.UserId,
            request.MakerUserId,
            "127.0.0.1",
            DateTime.UtcNow,
            null,
            payloadJson,
            "User creation proposed and pending checker approval.");

        await _adminRepository.AddAuditJournalAsync(journal, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapProposalToResponse(proposal);
    }

    public async Task<AdminProposalResponse> ApproveProposalAsync(Guid proposalId, ApproveProposalRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var proposal = await _adminRepository.GetProposalByIdAsync(proposalId, ct)
            ?? throw new KeyNotFoundException($"Admin proposal with ID '{proposalId}' was not found.");

        proposal.Approve(request.CheckerUserId, request.Remarks, DateTime.UtcNow);

        if (proposal.ActionType == AdminActionType.CreateUser)
        {
            var userReq = JsonSerializer.Deserialize<ProposeCreateUserRequest>(proposal.ProposalPayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Invalid proposal payload for CreateUser.");

            var newUser = new SystemUserProfile(
                userReq.UserId,
                userReq.EmployeeId,
                userReq.FullName,
                userReq.Email,
                userReq.BranchCode,
                userReq.UserGroup,
                userReq.AssignedRoles,
                userReq.InitialPassword,
                userReq.DoaLimits,
                userReq.MakerUserId,
                DateTime.UtcNow);

            newUser.MarkApproved(request.CheckerUserId, DateTime.UtcNow);
            await _adminRepository.AddUserAsync(newUser, ct);
        }

        await _adminRepository.UpdateProposalAsync(proposal, ct);

        var journal = new AdminAuditJournal(
            Guid.NewGuid(),
            JournalCategory.UserProfileActivity,
            "ApproveAdminProposal",
            proposal.TargetEntityId,
            request.CheckerUserId,
            "127.0.0.1",
            DateTime.UtcNow,
            proposal.ProposalPayloadJson,
            proposal.Status.ToString(),
            request.Remarks);

        await _adminRepository.AddAuditJournalAsync(journal, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapProposalToResponse(proposal);
    }

    public async Task<AdminProposalResponse> RejectProposalAsync(Guid proposalId, RejectProposalRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var proposal = await _adminRepository.GetProposalByIdAsync(proposalId, ct)
            ?? throw new KeyNotFoundException($"Admin proposal with ID '{proposalId}' was not found.");

        proposal.Reject(request.CheckerUserId, request.Remarks, DateTime.UtcNow);
        await _adminRepository.UpdateProposalAsync(proposal, ct);

        var journal = new AdminAuditJournal(
            Guid.NewGuid(),
            JournalCategory.UserProfileActivity,
            "RejectAdminProposal",
            proposal.TargetEntityId,
            request.CheckerUserId,
            "127.0.0.1",
            DateTime.UtcNow,
            proposal.ProposalPayloadJson,
            proposal.Status.ToString(),
            request.Remarks);

        await _adminRepository.AddAuditJournalAsync(journal, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapProposalToResponse(proposal);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var user = await _adminRepository.GetUserByIdAsync(request.UserId, ct)
            ?? throw new KeyNotFoundException($"User with ID '{request.UserId}' was not found.");

        if (user.Status == UserAccountStatus.Locked)
        {
            throw new InvariantViolationException(
                "AccountLockedInvariant",
                "BRPD Circular 12/2024 (10.1.3)",
                "User account is locked due to consecutive failed login attempts.");
        }

        bool isPasswordValid = user.VerifyPassword(request.Password);
        string sessionToken = user.RecordLoginAttempt(isPasswordValid, request.WorkstationIp, DateTime.UtcNow,
            isPasswordValid ? null : "Invalid password entered.");

        await _adminRepository.UpdateUserAsync(user, ct);

        var journal = new AdminAuditJournal(
            Guid.NewGuid(),
            JournalCategory.UserProfileActivity,
            isPasswordValid ? "LoginSuccess" : "LoginFailure",
            user.Id,
            user.Id,
            request.WorkstationIp,
            DateTime.UtcNow,
            null,
            null,
            isPasswordValid ? "Session initialized." : "Incorrect credentials.");

        await _adminRepository.AddAuditJournalAsync(journal, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        if (!isPasswordValid)
        {
            throw new InvariantViolationException(
                "AuthenticationFailedInvariant",
                "BRPD Circular 12/2024 (10.1.3)",
                "Invalid credentials provided.");
        }

        string? alert = null;
        if (user.PreviousFailedLoginAtUtc.HasValue)
        {
            alert = $"Notice [BRPD 9.1.1-xviii]: Previous failed login attempt was at {user.PreviousFailedLoginAtUtc:yyyy-MM-dd HH:mm:ss} UTC from IP {user.PreviousFailedLoginIp ?? "Unknown"}.";
        }

        return new LoginResponse(
            user.Id,
            user.FullName,
            user.UserGroup.ToString(),
            sessionToken,
            user.MustChangePasswordOnNextLogin,
            user.IsPasswordExpired(DateTime.UtcNow),
            user.PreviousSuccessfulLoginAtUtc,
            user.PreviousSuccessfulLoginIp,
            user.PreviousFailedLoginAtUtc,
            user.PreviousFailedLoginIp,
            alert);
    }

    public async Task<SystemUserResponse> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var user = await _adminRepository.GetUserByIdAsync(request.UserId, ct)
            ?? throw new KeyNotFoundException($"User with ID '{request.UserId}' was not found.");

        user.ChangePassword(request.CurrentPassword, request.NewPassword, DateTime.UtcNow);
        await _adminRepository.UpdateUserAsync(user, ct);

        var journal = new AdminAuditJournal(
            Guid.NewGuid(),
            JournalCategory.UserProfileActivity,
            "ChangePassword",
            user.Id,
            user.Id,
            user.LastLoginWorkstationIp ?? "127.0.0.1",
            DateTime.UtcNow,
            null,
            null,
            "Password rotated in compliance with BRPD 10.1.3.");

        await _adminRepository.AddAuditJournalAsync(journal, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapUserToResponse(user);
    }

    public async Task<EvaluateDoaResponse> EvaluateDoaAsync(EvaluateDoaRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var user = await _adminRepository.GetUserByIdAsync(request.UserId, ct)
            ?? throw new KeyNotFoundException($"User with ID '{request.UserId}' was not found.");

        // BRPD 10.3.5-vii: Super/Admin users in CBS shall not be authorized to perform any financial transactions
        user.AssertCanPerformFinancialTransaction();

        var (isAllowed, userLimit, breachReason) = user.DoaLimits.EvaluateTransaction(
            request.TransactionAmount,
            request.IsCredit,
            request.IsInterBranch,
            request.IsGl);

        if (isAllowed)
        {
            return new EvaluateDoaResponse(true, false, userLimit, null, null);
        }

        // Limit breached -> generate TransactionDoaOverride
        string txnType = request.IsGl ? "GlPosting" : (request.IsInterBranch ? (request.IsCredit ? "InterBranchCredit" : "InterBranchDebit") : (request.IsCredit ? "LocalCredit" : "LocalDebit"));

        var overrideItem = new TransactionDoaOverride(
            Guid.NewGuid(),
            request.TransactionReference,
            request.AccountNumber,
            user.Id,
            request.TransactionAmount,
            txnType,
            userLimit,
            DateTime.UtcNow);

        await _adminRepository.AddOverrideAsync(overrideItem, ct);

        var journal = new AdminAuditJournal(
            Guid.NewGuid(),
            JournalCategory.DoaLimitOverride,
            "LimitBreachOverrideInitiated",
            overrideItem.Id.ToString(),
            user.Id,
            "127.0.0.1",
            DateTime.UtcNow,
            $"Limit: {userLimit}",
            $"Amount: {request.TransactionAmount}",
            breachReason);

        await _adminRepository.AddAuditJournalAsync(journal, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new EvaluateDoaResponse(false, true, userLimit, breachReason, overrideItem.Id);
    }

    public async Task<DoaOverrideResponse> AuthorizeOverrideAsync(Guid overrideId, AuthorizeOverrideRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var overrideItem = await _adminRepository.GetOverrideByIdAsync(overrideId, ct)
            ?? throw new KeyNotFoundException($"Transaction override with ID '{overrideId}' was not found.");

        var supervisor = await _adminRepository.GetUserByIdAsync(request.SupervisorUserId, ct)
            ?? throw new KeyNotFoundException($"Supervisor user '{request.SupervisorUserId}' was not found.");

        supervisor.AssertCanPerformFinancialTransaction();

        overrideItem.Authorize(request.SupervisorUserId, request.Remarks, DateTime.UtcNow);
        await _adminRepository.UpdateOverrideAsync(overrideItem, ct);

        var journal = new AdminAuditJournal(
            Guid.NewGuid(),
            JournalCategory.DoaLimitOverride,
            "LimitOverrideAuthorized",
            overrideItem.Id.ToString(),
            request.SupervisorUserId,
            "127.0.0.1",
            DateTime.UtcNow,
            overrideItem.UserLimit.ToString("N2"),
            overrideItem.TransactionAmount.ToString("N2"),
            request.Remarks);

        await _adminRepository.AddAuditJournalAsync(journal, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapOverrideToResponse(overrideItem);
    }

    public async Task<IEnumerable<AuditJournalResponse>> GetAuditJournalsAsync(
        JournalCategory? category,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var list = await _adminRepository.GetAuditJournalsAsync(category, fromUtc, toUtc, ct);
        return list.Select(j => new AuditJournalResponse(
            j.Id,
            j.Category.ToString(),
            j.Action,
            j.EntityId,
            j.ActorUserId,
            j.WorkstationIp,
            j.TimestampUtc,
            j.OldValueJson,
            j.NewValueJson,
            j.Remarks));
    }

    public async Task<SystemUserResponse> GetUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await _adminRepository.GetUserByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException($"User with ID '{userId}' was not found.");
        return MapUserToResponse(user);
    }

    private static AdminProposalResponse MapProposalToResponse(AdminActionProposal p) =>
        new(p.Id, p.ActionType.ToString(), p.TargetEntityId, p.ProposalPayloadJson, p.MakerUserId, p.ProposedAtUtc,
            p.Status.ToString(), p.CheckerUserId, p.ReviewedAtUtc, p.ReviewRemarks);

    private static DoaOverrideResponse MapOverrideToResponse(TransactionDoaOverride o) =>
        new(o.Id, o.TransactionReference, o.AccountNumber, o.InitiatorUserId, o.TransactionAmount, o.TransactionType,
            o.UserLimit, o.Status.ToString(), o.SupervisorUserId, o.AuthorizedAtUtc, o.SupervisorRemarks);

    private static SystemUserResponse MapUserToResponse(SystemUserProfile u) =>
        new(u.Id, u.EmployeeId, u.FullName, u.Email, u.BranchCode, u.UserGroup.ToString(), u.AssignedRoles,
            u.MustChangePasswordOnNextLogin, u.PasswordLastChangedAtUtc, u.Status.ToString(), u.DoaLimits);
}
