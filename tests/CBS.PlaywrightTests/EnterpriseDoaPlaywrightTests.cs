// ARCHITECTURAL INTENT: Playwright API Integration tests locking Enterprise Delegation of Authority (DOA),
// Multi-tier Transaction Limit Matrix, Temporary Acting Authority & Absence Delegation,
// and Password Lifecycle & History Invariants per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CBS.Application.Security.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class EnterpriseDoaPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public EnterpriseDoaPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DoaProfile_Configure_And_TransactionLimit_Validation_Flow_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var userId = $"TELLER-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var branchCode = $"BR-{Guid.NewGuid():N}"[..6].ToUpperInvariant();

        // 1. Configure DOA Profile for Branch Teller
        var configureReq = new ConfigureDoaProfileRequest(
            UserId: userId,
            RoleName: "BranchTeller",
            BranchCode: branchCode,
            MaxLocalDebitLimit: 500_000m,
            MaxLocalCreditLimit: 1_000_000m,
            MaxInterBranchDebitLimit: 200_000m,
            MaxInterBranchCreditLimit: 500_000m,
            MaxGlAdjustmentLimit: 0m,
            AssignedByUserId: "SEC-ADMIN-01");

        var configRes = await api.PostAsync("/api/v1/admin/doa/profiles", new() { DataObject = configureReq });
        configRes.Ok.Should().BeTrue();
        configRes.Status.Should().Be(200);

        var profileDto = JsonSerializer.Deserialize<UserDoaProfileResponseDto>(await configRes.TextAsync(), JsonOptions);
        profileDto.Should().NotBeNull();
        profileDto!.UserId.Should().Be(userId);
        profileDto.RoleName.Should().Be("BranchTeller");
        profileDto.MaxLocalDebitLimit.Should().Be(500_000m);
        profileDto.MaxLocalCreditLimit.Should().Be(1_000_000m);
        profileDto.MaxInterBranchDebitLimit.Should().Be(200_000m);
        profileDto.MaxInterBranchCreditLimit.Should().Be(500_000m);
        profileDto.MaxGlAdjustmentLimit.Should().Be(0m);
        profileDto.IsActive.Should().BeTrue();

        // 2. Fetch DOA Profile via GET
        var getProfileRes = await api.GetAsync($"/api/v1/admin/doa/profiles/{userId}");
        getProfileRes.Ok.Should().BeTrue();
        var fetchedProfile = JsonSerializer.Deserialize<UserDoaProfileResponseDto>(await getProfileRes.TextAsync(), JsonOptions);
        fetchedProfile.Should().NotBeNull();
        fetchedProfile!.UserId.Should().Be(userId);

        // 3. Validate Transaction within limit (Local Debit 350,000 <= 500,000)
        var validDebitReq = new ValidateTransactionDoaRequest(
            UserId: userId,
            LimitType: TransactionLimitType.LocalDebit,
            Amount: 350_000m);

        var validDebitRes = await api.PostAsync("/api/v1/admin/doa/validate-limit", new() { DataObject = validDebitReq });
        validDebitRes.Ok.Should().BeTrue();

        var validDebitDto = JsonSerializer.Deserialize<ValidateTransactionDoaResponseDto>(await validDebitRes.TextAsync(), JsonOptions);
        validDebitDto.Should().NotBeNull();
        validDebitDto!.IsAllowed.Should().BeTrue();
        validDebitDto.RequiresHigherApproval.Should().BeFalse();
        validDebitDto.CeilingAmount.Should().Be(500_000m);

        // 4. Validate Transaction exceeding limit (Local Debit 750,000 > 500,000) -> Escalation
        var excessDebitReq = new ValidateTransactionDoaRequest(
            UserId: userId,
            LimitType: TransactionLimitType.LocalDebit,
            Amount: 750_000m);

        var excessDebitRes = await api.PostAsync("/api/v1/admin/doa/validate-limit", new() { DataObject = excessDebitReq });
        excessDebitRes.Ok.Should().BeTrue();

        var excessDebitDto = JsonSerializer.Deserialize<ValidateTransactionDoaResponseDto>(await excessDebitRes.TextAsync(), JsonOptions);
        excessDebitDto.Should().NotBeNull();
        excessDebitDto!.IsAllowed.Should().BeFalse();
        excessDebitDto.RequiresHigherApproval.Should().BeTrue();
        excessDebitDto.CeilingAmount.Should().Be(500_000m);
        excessDebitDto.Message.Should().Contain("TransactionDoaCeilingInvariant");

        // 5. Validate GL Adjustment when Ceiling is 0
        var glReq = new ValidateTransactionDoaRequest(
            UserId: userId,
            LimitType: TransactionLimitType.GlManualAdjustment,
            Amount: 100m);

        var glRes = await api.PostAsync("/api/v1/admin/doa/validate-limit", new() { DataObject = glReq });
        glRes.Ok.Should().BeTrue();

        var glDto = JsonSerializer.Deserialize<ValidateTransactionDoaResponseDto>(await glRes.TextAsync(), JsonOptions);
        glDto.Should().NotBeNull();
        glDto!.IsAllowed.Should().BeFalse();
        glDto.RequiresHigherApproval.Should().BeTrue();
        glDto.CeilingAmount.Should().Be(0m);
    }

    [Fact]
    public async Task TemporaryAuthorityDelegation_Request_Approve_And_Revoke_Flow_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var delegatorId = $"MGR-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var delegateeId = $"OFFICER-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var branchCode = $"BR-{Guid.NewGuid():N}"[..6].ToUpperInvariant();

        // 1. Prohibit Self-Delegation (Delegator == Delegatee)
        var selfDelegationReq = new CreateTemporaryDelegationRequest(
            DelegatorUserId: delegatorId,
            DelegateeUserId: delegatorId,
            DelegatedRole: "BranchManager",
            BranchCode: branchCode,
            EffectiveFromUtc: DateTime.UtcNow,
            EffectiveToUtc: DateTime.UtcNow.AddDays(5),
            DelegationReason: "Attempting self delegation");

        var badSelfRes = await api.PostAsync("/api/v1/admin/delegations/request", new() { DataObject = selfDelegationReq });
        badSelfRes.Ok.Should().BeFalse();
        badSelfRes.Status.Should().Be(400);
        var selfErrText = await badSelfRes.TextAsync();
        selfErrText.Should().Contain("SelfDelegationForbiddenInvariant");

        // 2. Request Valid Temporary Delegation
        var validReq = new CreateTemporaryDelegationRequest(
            DelegatorUserId: delegatorId,
            DelegateeUserId: delegateeId,
            DelegatedRole: "BranchManager",
            BranchCode: branchCode,
            EffectiveFromUtc: DateTime.UtcNow.AddMinutes(-5),
            EffectiveToUtc: DateTime.UtcNow.AddDays(7),
            DelegationReason: "Branch Manager on annual leave");

        var createRes = await api.PostAsync("/api/v1/admin/delegations/request", new() { DataObject = validReq });
        createRes.Ok.Should().BeTrue();
        createRes.Status.Should().Be(201);

        var createdDto = JsonSerializer.Deserialize<TemporaryDelegationResponseDto>(await createRes.TextAsync(), JsonOptions);
        createdDto.Should().NotBeNull();
        var delegationId = createdDto!.DelegationId;
        createdDto.Status.Should().Be(DelegationStatus.PendingApproval.ToString());

        // 3. Prohibit Delegator Approving Own Delegation (Dual-Custody Invariant)
        var badApproveReq = new ApproveTemporaryDelegationRequest(ApproverUserId: delegatorId);
        var badApproveRes = await api.PostAsync($"/api/v1/admin/delegations/{delegationId}/approve", new() { DataObject = badApproveReq });
        badApproveRes.Ok.Should().BeFalse();
        badApproveRes.Status.Should().Be(400);
        var badApproveText = await badApproveRes.TextAsync();
        badApproveText.Should().Contain("DelegationDualCustodyInvariant");

        // 4. Independent Approver Approves Delegation
        var goodApproveReq = new ApproveTemporaryDelegationRequest(ApproverUserId: "REGIONAL-HEAD-01");
        var goodApproveRes = await api.PostAsync($"/api/v1/admin/delegations/{delegationId}/approve", new() { DataObject = goodApproveReq });
        goodApproveRes.Ok.Should().BeTrue();

        var approvedDto = JsonSerializer.Deserialize<TemporaryDelegationResponseDto>(await goodApproveRes.TextAsync(), JsonOptions);
        approvedDto.Should().NotBeNull();
        approvedDto!.Status.Should().Be(DelegationStatus.Active.ToString());
        approvedDto.ApprovedByUserId.Should().Be("REGIONAL-HEAD-01");
        approvedDto.ApprovedAtUtc.Should().NotBeNull();

        // 5. Query Active Delegations
        var activeRes = await api.GetAsync("/api/v1/admin/delegations/active");
        activeRes.Ok.Should().BeTrue();
        var activeList = JsonSerializer.Deserialize<List<TemporaryDelegationResponseDto>>(await activeRes.TextAsync(), JsonOptions);
        activeList.Should().NotBeNull();
        activeList.Should().Contain(d => d.DelegationId == delegationId);

        // 6. Early Revocation of Delegation
        var revokeReq = new RevokeTemporaryDelegationRequest(
            RevokedByUserId: "REGIONAL-HEAD-01",
            Reason: "Manager resumed duties early");

        var revokeRes = await api.PostAsync($"/api/v1/admin/delegations/{delegationId}/revoke", new() { DataObject = revokeReq });
        revokeRes.Ok.Should().BeTrue();

        var revokedDto = JsonSerializer.Deserialize<TemporaryDelegationResponseDto>(await revokeRes.TextAsync(), JsonOptions);
        revokedDto.Should().NotBeNull();
        revokedDto!.Status.Should().Be(DelegationStatus.Revoked.ToString());
        revokedDto.RevokedByUserId.Should().Be("REGIONAL-HEAD-01");
        revokedDto.RevocationReason.Should().Be("Manager resumed duties early");
    }

    [Fact]
    public async Task PasswordLifecycle_AdminReset_Change_And_HistoryLock_Flow_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var userId = $"USER-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        // 1. Admin Resets User Password (Initial Temporary Password)
        var adminResetReq = new AdminResetPasswordRequest(
            UserId: userId,
            TemporaryPasswordHash: "HASH_INIT_TEMP_001",
            TemporarySalt: "SALT_TEMP_001",
            AdminUserId: "SEC-ADMIN-01");

        var resetRes = await api.PostAsync("/api/v1/admin/security/password/admin-reset", new() { DataObject = adminResetReq });
        resetRes.Ok.Should().BeTrue();

        var resetDto = JsonSerializer.Deserialize<PasswordOperationResponseDto>(await resetRes.TextAsync(), JsonOptions);
        resetDto.Should().NotBeNull();
        resetDto!.UserId.Should().Be(userId);
        resetDto.Success.Should().BeTrue();
        resetDto.IsFirstLoginPendingChange.Should().BeTrue();

        // 2. User Changes Initial Temporary Password to Password V1
        var changeV1Req = new ChangeUserPasswordRequest(
            UserId: userId,
            CurrentPasswordHash: "HASH_INIT_TEMP_001",
            NewPasswordHash: "HASH_V1_COMPLEX_P@SS1",
            NewSalt: "SALT_V1");

        var changeV1Res = await api.PostAsync("/api/v1/admin/security/password/change", new() { DataObject = changeV1Req });
        changeV1Res.Ok.Should().BeTrue();

        var changeV1Dto = JsonSerializer.Deserialize<PasswordOperationResponseDto>(await changeV1Res.TextAsync(), JsonOptions);
        changeV1Dto.Should().NotBeNull();
        changeV1Dto!.IsFirstLoginPendingChange.Should().BeFalse();

        // 3. Attempting to reuse Current Password (or recent history) must violate PasswordHistoryFourTimesInvariant
        var reuseReq = new ChangeUserPasswordRequest(
            UserId: userId,
            CurrentPasswordHash: "HASH_V1_COMPLEX_P@SS1",
            NewPasswordHash: "HASH_V1_COMPLEX_P@SS1", // Reusing current
            NewSalt: "SALT_V1_REUSE");

        var badReuseRes = await api.PostAsync("/api/v1/admin/security/password/change", new() { DataObject = reuseReq });
        badReuseRes.Ok.Should().BeFalse();
        badReuseRes.Status.Should().Be(400);

        var reuseErrText = await badReuseRes.TextAsync();
        reuseErrText.Should().Contain("PasswordHistoryFourTimesInvariant");

        // 4. Change through V2, V3, V4, V5 (Rotating 4 distinct passwords)
        var changeV2Req = new ChangeUserPasswordRequest(userId, "HASH_V1_COMPLEX_P@SS1", "HASH_V2_COMPLEX_P@SS2", "SALT_V2");
        (await api.PostAsync("/api/v1/admin/security/password/change", new() { DataObject = changeV2Req })).Ok.Should().BeTrue();

        var changeV3Req = new ChangeUserPasswordRequest(userId, "HASH_V2_COMPLEX_P@SS2", "HASH_V3_COMPLEX_P@SS3", "SALT_V3");
        (await api.PostAsync("/api/v1/admin/security/password/change", new() { DataObject = changeV3Req })).Ok.Should().BeTrue();

        var changeV4Req = new ChangeUserPasswordRequest(userId, "HASH_V3_COMPLEX_P@SS3", "HASH_V4_COMPLEX_P@SS4", "SALT_V4");
        (await api.PostAsync("/api/v1/admin/security/password/change", new() { DataObject = changeV4Req })).Ok.Should().BeTrue();

        var changeV5Req = new ChangeUserPasswordRequest(userId, "HASH_V4_COMPLEX_P@SS4", "HASH_V5_COMPLEX_P@SS5", "SALT_V5");
        (await api.PostAsync("/api/v1/admin/security/password/change", new() { DataObject = changeV5Req })).Ok.Should().BeTrue();

        // 5. After 4 changes (current = V5, history = V4, V3, V2, V1), older temporary password (HASH_INIT_TEMP_001) has rolled off and can be reused
        var recycleOldReq = new ChangeUserPasswordRequest(
            UserId: userId,
            CurrentPasswordHash: "HASH_V5_COMPLEX_P@SS5",
            NewPasswordHash: "HASH_INIT_TEMP_001",
            NewSalt: "SALT_RECYCLED");

        var recycleRes = await api.PostAsync("/api/v1/admin/security/password/change", new() { DataObject = recycleOldReq });
        recycleRes.Ok.Should().BeTrue();

        var recycledDto = JsonSerializer.Deserialize<PasswordOperationResponseDto>(await recycleRes.TextAsync(), JsonOptions);
        recycledDto.Should().NotBeNull();
        recycledDto!.Success.Should().BeTrue();
    }
}
