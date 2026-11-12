using System;
using System.Collections.Generic;
using CBS.Domain.Enums;

namespace CBS.Application.Security.DTOs;

// --- 1. Delegation of Authority (DOA) DTOs ---

public sealed record ConfigureDoaProfileRequest(
    string UserId,
    string RoleName,
    string BranchCode,
    decimal MaxLocalDebitLimit,
    decimal MaxLocalCreditLimit,
    decimal MaxInterBranchDebitLimit,
    decimal MaxInterBranchCreditLimit,
    decimal MaxGlAdjustmentLimit,
    string AssignedByUserId);

public sealed record UserDoaProfileResponseDto(
    string ProfileId,
    string UserId,
    string RoleName,
    string BranchCode,
    decimal MaxLocalDebitLimit,
    decimal MaxLocalCreditLimit,
    decimal MaxInterBranchDebitLimit,
    decimal MaxInterBranchCreditLimit,
    decimal MaxGlAdjustmentLimit,
    bool IsActive,
    string AssignedByUserId,
    DateTime AssignedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record ValidateTransactionDoaRequest(
    string UserId,
    TransactionLimitType LimitType,
    decimal Amount);

public sealed record ValidateTransactionDoaResponseDto(
    string UserId,
    string LimitType,
    decimal Amount,
    bool IsAllowed,
    string EffectiveRole,
    decimal CeilingAmount,
    bool RequiresHigherApproval,
    string? Message);


// --- 2. Temporary Authority Delegation DTOs ---

public sealed record CreateTemporaryDelegationRequest(
    string DelegatorUserId,
    string DelegateeUserId,
    string DelegatedRole,
    string BranchCode,
    DateTime EffectiveFromUtc,
    DateTime EffectiveToUtc,
    string DelegationReason);

public sealed record ApproveTemporaryDelegationRequest(
    string ApproverUserId);

public sealed record RevokeTemporaryDelegationRequest(
    string RevokedByUserId,
    string Reason);

public sealed record TemporaryDelegationResponseDto(
    string DelegationId,
    string DelegatorUserId,
    string DelegateeUserId,
    string DelegatedRole,
    string BranchCode,
    DateTime EffectiveFromUtc,
    DateTime EffectiveToUtc,
    string DelegationReason,
    string Status,
    string? ApprovedByUserId,
    DateTime? ApprovedAtUtc,
    string? RevokedByUserId,
    string? RevocationReason,
    DateTime? RevokedAtUtc,
    DateTime CreatedAtUtc);


// --- 3. Password Lifecycle DTOs ---

public sealed record ChangeUserPasswordRequest(
    string UserId,
    string CurrentPasswordHash,
    string NewPasswordHash,
    string NewSalt);

public sealed record AdminResetPasswordRequest(
    string UserId,
    string TemporaryPasswordHash,
    string TemporarySalt,
    string AdminUserId);

public sealed record PasswordOperationResponseDto(
    string UserId,
    bool Success,
    string Message,
    bool IsFirstLoginPendingChange,
    DateTime LastChangedAtUtc);
