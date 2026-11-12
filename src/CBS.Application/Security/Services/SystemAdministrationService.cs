using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Application.Security.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Security;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Security.Services;

public sealed class SystemAdministrationService : ISystemAdministrationService
{
    private readonly IUserDoaProfileRepository _doaRepository;
    private readonly ITemporaryDelegationRepository _delegationRepository;
    private readonly IUserPasswordCredentialRepository _passwordRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SystemAdministrationService(
        IUserDoaProfileRepository doaRepository,
        ITemporaryDelegationRepository delegationRepository,
        IUserPasswordCredentialRepository passwordRepository,
        IUnitOfWork unitOfWork)
    {
        _doaRepository = doaRepository;
        _delegationRepository = delegationRepository;
        _passwordRepository = passwordRepository;
        _unitOfWork = unitOfWork;
    }

    // =========================================================================
    // 1. Delegation of Authority (DOA) Matrix
    // =========================================================================

    public async Task<UserDoaProfileResponseDto> ConfigureDoaProfileAsync(
        ConfigureDoaProfileRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var existing = await _doaRepository.GetByUserIdAsync(request.UserId, ct);
        if (existing != null)
        {
            existing.UpdateLimits(
                Money.Bdt(request.MaxLocalDebitLimit),
                Money.Bdt(request.MaxLocalCreditLimit),
                Money.Bdt(request.MaxInterBranchDebitLimit),
                Money.Bdt(request.MaxInterBranchCreditLimit),
                Money.Bdt(request.MaxGlAdjustmentLimit),
                request.AssignedByUserId);

            await _doaRepository.UpdateAsync(existing, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return MapToDoaDto(existing);
        }

        string profileId = $"DOA-{request.UserId.Trim().ToUpperInvariant()}";
        var profile = new UserDoaProfile(
            profileId: profileId,
            userId: request.UserId,
            roleName: request.RoleName,
            branchCode: request.BranchCode,
            maxLocalDebitLimit: Money.Bdt(request.MaxLocalDebitLimit),
            maxLocalCreditLimit: Money.Bdt(request.MaxLocalCreditLimit),
            maxInterBranchDebitLimit: Money.Bdt(request.MaxInterBranchDebitLimit),
            maxInterBranchCreditLimit: Money.Bdt(request.MaxInterBranchCreditLimit),
            maxGlAdjustmentLimit: Money.Bdt(request.MaxGlAdjustmentLimit),
            assignedByUserId: request.AssignedByUserId,
            assignedAtUtc: DateTime.UtcNow);

        await _doaRepository.AddAsync(profile, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDoaDto(profile);
    }

    public async Task<UserDoaProfileResponseDto?> GetDoaProfileAsync(string userId, CancellationToken ct = default)
    {
        var profile = await _doaRepository.GetByUserIdAsync(userId, ct);
        return profile == null ? null : MapToDoaDto(profile);
    }

    public async Task<ValidateTransactionDoaResponseDto> ValidateTransactionDoaAsync(
        ValidateTransactionDoaRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var now = DateTime.UtcNow;

        // Check if user has active acting authority delegation
        var activeDelegation = await _delegationRepository.GetActiveDelegationAsync(request.UserId, now, ct);
        UserDoaProfile? effectiveProfile = null;
        string effectiveRole = "STANDARD_USER";

        if (activeDelegation != null)
        {
            // Inherit delegator's DOA profile during active absence window
            effectiveProfile = await _doaRepository.GetByUserIdAsync(activeDelegation.DelegatorUserId, ct);
            effectiveRole = $"ACTING_{activeDelegation.DelegatedRole}";
        }

        if (effectiveProfile == null)
        {
            effectiveProfile = await _doaRepository.GetByUserIdAsync(request.UserId, ct);
            if (effectiveProfile != null)
            {
                effectiveRole = effectiveProfile.RoleName;
            }
        }

        if (effectiveProfile == null)
        {
            return new ValidateTransactionDoaResponseDto(
                UserId: request.UserId,
                LimitType: request.LimitType.ToString(),
                Amount: request.Amount,
                IsAllowed: false,
                EffectiveRole: "UNASSIGNED",
                CeilingAmount: 0m,
                RequiresHigherApproval: true,
                Message: $"No Delegation of Authority profile found for user '{request.UserId}'. Higher supervisor approval required.");
        }

        Money applicableCeiling = request.LimitType switch
        {
            TransactionLimitType.LocalDebit => effectiveProfile.MaxLocalDebitLimit,
            TransactionLimitType.LocalCredit => effectiveProfile.MaxLocalCreditLimit,
            TransactionLimitType.InterBranchDebit => effectiveProfile.MaxInterBranchDebitLimit,
            TransactionLimitType.InterBranchCredit => effectiveProfile.MaxInterBranchCreditLimit,
            TransactionLimitType.GlManualAdjustment => effectiveProfile.MaxGlAdjustmentLimit,
            _ => throw new ArgumentOutOfRangeException(nameof(request.LimitType))
        };

        try
        {
            effectiveProfile.AssertTransactionAllowed(request.LimitType, Money.Bdt(request.Amount));
            return new ValidateTransactionDoaResponseDto(
                UserId: request.UserId,
                LimitType: request.LimitType.ToString(),
                Amount: request.Amount,
                IsAllowed: true,
                EffectiveRole: effectiveRole,
                CeilingAmount: applicableCeiling.Amount,
                RequiresHigherApproval: false,
                Message: "Transaction within authorized Delegation of Authority limit.");
        }
        catch (InvariantViolationException ex)
        {
            return new ValidateTransactionDoaResponseDto(
                UserId: request.UserId,
                LimitType: request.LimitType.ToString(),
                Amount: request.Amount,
                IsAllowed: false,
                EffectiveRole: effectiveRole,
                CeilingAmount: applicableCeiling.Amount,
                RequiresHigherApproval: true,
                Message: ex.Message);
        }
    }

    // =========================================================================
    // 2. Temporary Authority & Absence Delegation
    // =========================================================================

    public async Task<TemporaryDelegationResponseDto> CreateTemporaryDelegationAsync(
        CreateTemporaryDelegationRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        string delegationId = $"DEL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var delegation = new TemporaryAuthorityDelegation(
            delegationId: delegationId,
            delegatorUserId: request.DelegatorUserId,
            delegateeUserId: request.DelegateeUserId,
            delegatedRole: request.DelegatedRole,
            branchCode: request.BranchCode,
            effectiveFromUtc: request.EffectiveFromUtc,
            effectiveToUtc: request.EffectiveToUtc,
            delegationReason: request.DelegationReason,
            createdAtUtc: DateTime.UtcNow);

        await _delegationRepository.AddAsync(delegation, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDelegationDto(delegation);
    }

    public async Task<TemporaryDelegationResponseDto> ApproveTemporaryDelegationAsync(
        string delegationId,
        ApproveTemporaryDelegationRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var delegation = await _delegationRepository.GetByIdAsync(delegationId, ct)
            ?? throw new KeyNotFoundException($"Delegation '{delegationId}' was not found.");

        delegation.Approve(request.ApproverUserId);
        await _delegationRepository.UpdateAsync(delegation, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDelegationDto(delegation);
    }

    public async Task<TemporaryDelegationResponseDto> RevokeTemporaryDelegationAsync(
        string delegationId,
        RevokeTemporaryDelegationRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var delegation = await _delegationRepository.GetByIdAsync(delegationId, ct)
            ?? throw new KeyNotFoundException($"Delegation '{delegationId}' was not found.");

        delegation.Revoke(request.RevokedByUserId, request.Reason);
        await _delegationRepository.UpdateAsync(delegation, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDelegationDto(delegation);
    }

    public async Task<IReadOnlyList<TemporaryDelegationResponseDto>> GetActiveDelegationsAsync(CancellationToken ct = default)
    {
        var list = await _delegationRepository.GetActiveDelegationsAsync(ct);
        return list.Select(MapToDelegationDto).ToList();
    }

    // =========================================================================
    // 3. Password Lifecycle & History Invariants
    // =========================================================================

    public async Task<PasswordOperationResponseDto> ChangeUserPasswordAsync(
        ChangeUserPasswordRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var credential = await _passwordRepository.GetByUserIdAsync(request.UserId, ct);
        if (credential == null)
        {
            // Initial credential registration
            credential = new UserPasswordCredential(
                userId: request.UserId,
                passwordHash: request.NewPasswordHash,
                salt: request.NewSalt,
                isFirstLoginPendingChange: false,
                createdAtUtc: DateTime.UtcNow);

            await _passwordRepository.AddAsync(credential, ct);
        }
        else
        {
            credential.ChangePassword(request.NewPasswordHash, request.NewSalt, DateTime.UtcNow);
            await _passwordRepository.UpdateAsync(credential, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new PasswordOperationResponseDto(
            UserId: credential.UserId,
            Success: true,
            Message: "Password changed successfully conforming to BRPD 10.1.3 password history requirements.",
            IsFirstLoginPendingChange: credential.IsFirstLoginPendingChange,
            LastChangedAtUtc: credential.LastChangedAtUtc);
    }

    public async Task<PasswordOperationResponseDto> AdminResetPasswordAsync(
        AdminResetPasswordRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var credential = await _passwordRepository.GetByUserIdAsync(request.UserId, ct);
        if (credential == null)
        {
            credential = new UserPasswordCredential(
                userId: request.UserId,
                passwordHash: request.TemporaryPasswordHash,
                salt: request.TemporarySalt,
                isFirstLoginPendingChange: true,
                createdAtUtc: DateTime.UtcNow);

            await _passwordRepository.AddAsync(credential, ct);
        }
        else
        {
            credential.AdminResetPassword(request.TemporaryPasswordHash, request.TemporarySalt, DateTime.UtcNow);
            await _passwordRepository.UpdateAsync(credential, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new PasswordOperationResponseDto(
            UserId: credential.UserId,
            Success: true,
            Message: "Temporary password set by admin. Mandatory reset required upon first login.",
            IsFirstLoginPendingChange: true,
            LastChangedAtUtc: credential.LastChangedAtUtc);
    }

    // =========================================================================
    // DTO Mappings
    // =========================================================================

    private static UserDoaProfileResponseDto MapToDoaDto(UserDoaProfile p) =>
        new(p.ProfileId,
            p.UserId,
            p.RoleName,
            p.BranchCode,
            p.MaxLocalDebitLimit.Amount,
            p.MaxLocalCreditLimit.Amount,
            p.MaxInterBranchDebitLimit.Amount,
            p.MaxInterBranchCreditLimit.Amount,
            p.MaxGlAdjustmentLimit.Amount,
            p.IsActive,
            p.AssignedByUserId,
            p.AssignedAtUtc,
            p.UpdatedAtUtc);

    private static TemporaryDelegationResponseDto MapToDelegationDto(TemporaryAuthorityDelegation d) =>
        new(d.DelegationId,
            d.DelegatorUserId,
            d.DelegateeUserId,
            d.DelegatedRole,
            d.BranchCode,
            d.EffectiveFromUtc,
            d.EffectiveToUtc,
            d.DelegationReason,
            d.Status.ToString(),
            d.ApprovedByUserId,
            d.ApprovedAtUtc,
            d.RevokedByUserId,
            d.RevocationReason,
            d.RevokedAtUtc,
            d.CreatedAtUtc);
}
