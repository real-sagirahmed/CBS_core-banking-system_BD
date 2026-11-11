using CBS.Domain.Enums;
using CBS.Domain.Security.ValueObjects;

namespace CBS.Application.Security.DTOs;

public record ProposeCreateUserRequest(
    string UserId,
    string EmployeeId,
    string FullName,
    string Email,
    string BranchCode,
    UserGroupType UserGroup,
    List<string> AssignedRoles,
    string InitialPassword,
    DoaLimitProfile? DoaLimits,
    string MakerUserId);

public record ApproveProposalRequest(
    string CheckerUserId,
    string Remarks);

public record RejectProposalRequest(
    string CheckerUserId,
    string Remarks);

public record AdminProposalResponse(
    Guid Id,
    string ActionType,
    string TargetEntityId,
    string ProposalPayloadJson,
    string MakerUserId,
    DateTime ProposedAtUtc,
    string Status,
    string? CheckerUserId,
    DateTime? ReviewedAtUtc,
    string? ReviewRemarks);

public record ChangePasswordRequest(
    string UserId,
    string CurrentPassword,
    string NewPassword);

public record LoginRequest(
    string UserId,
    string Password,
    string WorkstationIp);

public record LoginResponse(
    string UserId,
    string FullName,
    string UserGroup,
    string ActiveSessionToken,
    bool MustChangePasswordOnNextLogin,
    bool IsPasswordExpired,
    DateTime? PreviousSuccessfulLoginAtUtc,
    string? PreviousSuccessfulLoginIp,
    DateTime? PreviousFailedLoginAtUtc,
    string? PreviousFailedLoginIp,
    string? SecurityAlertMessage);

public record EvaluateDoaRequest(
    string UserId,
    decimal TransactionAmount,
    bool IsCredit,
    bool IsInterBranch,
    bool IsGl,
    string TransactionReference,
    string AccountNumber);

public record EvaluateDoaResponse(
    bool IsAllowed,
    bool RequiresOverride,
    decimal UserLimit,
    string? BreachReason,
    Guid? OverrideId);

public record AuthorizeOverrideRequest(
    string SupervisorUserId,
    string Remarks);

public record DoaOverrideResponse(
    Guid Id,
    string TransactionReference,
    string AccountNumber,
    string InitiatorUserId,
    decimal TransactionAmount,
    string TransactionType,
    decimal UserLimit,
    string Status,
    string? SupervisorUserId,
    DateTime? AuthorizedAtUtc,
    string? SupervisorRemarks);

public record SystemUserResponse(
    string UserId,
    string EmployeeId,
    string FullName,
    string Email,
    string BranchCode,
    string UserGroup,
    List<string> AssignedRoles,
    bool MustChangePasswordOnNextLogin,
    DateTime PasswordLastChangedAtUtc,
    string Status,
    DoaLimitProfile DoaLimits);

public record AuditJournalResponse(
    Guid Id,
    string Category,
    string Action,
    string EntityId,
    string ActorUserId,
    string WorkstationIp,
    DateTime TimestampUtc,
    string? OldValueJson,
    string? NewValueJson,
    string? Remarks);
