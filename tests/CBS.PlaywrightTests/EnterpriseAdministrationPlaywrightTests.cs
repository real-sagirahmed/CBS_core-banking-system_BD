// ARCHITECTURAL INTENT: Playwright API Integration tests locking Administrative SoD Financial Restriction,
// Concurrent Single-Session Enforcement & Terminal Guard, and Admin Maker-Checker Dual-Control Parameters Engine
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10 (10.3.5-vii, viii, x; 10.3.2).

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

public class EnterpriseAdministrationPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public EnterpriseAdministrationPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AdminFinancialRestriction_Should_Block_Admin_Role_From_Financial_Transaction_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Attempting financial transaction as Administrator must return 403 Forbidden
        var headers = new Dictionary<string, string>
        {
            { "X-User-Id", "ADMIN-USER-01" },
            { "X-User-Role", "Administrator" },
            { "X-Terminal-Ip", "192.168.1.100" }
        };

        var postRes = await api.PostAsync("/api/v1/accounts/0100123456789/transactions", new()
        {
            Headers = headers,
            DataObject = new
            {
                AccountNumber = "0100123456789",
                TransactionType = "Deposit",
                Amount = 10_000m,
                Narration = "Admin attempting financial deposit"
            }
        });

        postRes.Status.Should().Be(403);
        var errText = await postRes.TextAsync();
        errText.Should().Contain("AdminFinancialTransactionProhibitedInvariant");
        errText.Should().Contain("10.3.5-vii");

        // 2. Attempting financial transaction as SuperAdmin must also return 403 Forbidden
        var superHeaders = new Dictionary<string, string>
        {
            { "X-User-Id", "SUPERADMIN-01" },
            { "X-User-Role", "SuperAdmin" },
            { "X-Terminal-Ip", "192.168.1.101" }
        };

        var superRes = await api.PostAsync("/api/v1/accounts/0100123456789/transactions", new()
        {
            Headers = superHeaders,
            DataObject = new
            {
                AccountNumber = "0100123456789",
                TransactionType = "Deposit",
                Amount = 5_000m,
                Narration = "SuperAdmin attempting financial deposit"
            }
        });

        superRes.Status.Should().Be(403);
        var superErrText = await superRes.TextAsync();
        superErrText.Should().Contain("AdminFinancialTransactionProhibitedInvariant");
    }

    [Fact]
    public async Task ConcurrentSession_Authenticate_Block_And_TerminatePrior_Flow_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var userId = $"OFFICER-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // 1. Initial Login with BlockNewSession Policy
        var login1 = new LoginWithSessionControlRequest(
            UserId: userId,
            RoleName: "GeneralOfficer",
            TerminalIp: "192.168.10.15",
            AppServerIp: "10.0.0.1",
            BranchCode: "BR-001",
            Policy: ConcurrentSessionPolicy.BlockNewSession);

        var login1Res = await api.PostAsync("/api/v1/admin/sessions/authenticate", new() { DataObject = login1 });
        login1Res.Ok.Should().BeTrue();

        var session1Dto = JsonSerializer.Deserialize<UserActiveSessionResponseDto>(await login1Res.TextAsync(), JsonOptions);
        session1Dto.Should().NotBeNull();
        session1Dto!.UserId.Should().Be(userId);
        session1Dto.TerminalIp.Should().Be("192.168.10.15");
        session1Dto.IsActive.Should().BeTrue();
        var sessionId1 = session1Dto.SessionId;

        // 2. Second Concurrent Login attempt from Terminal 2 with BlockNewSession policy -> Must be Rejected
        var login2Blocked = new LoginWithSessionControlRequest(
            UserId: userId,
            RoleName: "GeneralOfficer",
            TerminalIp: "192.168.10.99",
            AppServerIp: "10.0.0.1",
            BranchCode: "BR-001",
            Policy: ConcurrentSessionPolicy.BlockNewSession);

        var login2Res = await api.PostAsync("/api/v1/admin/sessions/authenticate", new() { DataObject = login2Blocked });
        login2Res.Ok.Should().BeFalse();
        login2Res.Status.Should().Be(400);

        var blockErr = await login2Res.TextAsync();
        blockErr.Should().Contain("ConcurrentSessionDisallowedInvariant");
        blockErr.Should().Contain("Multiple concurrent sessions are prohibited");

        // 3. Third Login attempt with TerminatePriorSession policy -> Supersedes Session 1
        var login3Supersede = new LoginWithSessionControlRequest(
            UserId: userId,
            RoleName: "GeneralOfficer",
            TerminalIp: "192.168.10.50",
            AppServerIp: "10.0.0.1",
            BranchCode: "BR-001",
            Policy: ConcurrentSessionPolicy.TerminatePriorSession);

        var login3Res = await api.PostAsync("/api/v1/admin/sessions/authenticate", new() { DataObject = login3Supersede });
        login3Res.Ok.Should().BeTrue();

        var session3Dto = JsonSerializer.Deserialize<UserActiveSessionResponseDto>(await login3Res.TextAsync(), JsonOptions);
        session3Dto.Should().NotBeNull();
        session3Dto!.SessionId.Should().NotBe(sessionId1);
        session3Dto.TerminalIp.Should().Be("192.168.10.50");
        session3Dto.IsActive.Should().BeTrue();

        // 4. Query Active Sessions -> Must include Session 3
        var activeRes = await api.GetAsync("/api/v1/admin/sessions/active");
        activeRes.Ok.Should().BeTrue();
        var activeList = JsonSerializer.Deserialize<List<UserActiveSessionResponseDto>>(await activeRes.TextAsync(), JsonOptions);
        activeList.Should().NotBeNull();
        activeList.Should().Contain(s => s.SessionId == session3Dto.SessionId);
        activeList.Should().NotContain(s => s.SessionId == sessionId1); // Session 1 was superseded and terminated

        // 5. Explicit Logout / Termination of Session 3
        var terminateReq = new TerminateSessionRequest(
            TerminatedByUserId: userId,
            Reason: SessionTerminationReason.UserLogout,
            Notes: "End of daily shift");

        var termRes = await api.PostAsync($"/api/v1/admin/sessions/{session3Dto.SessionId}/terminate", new() { DataObject = terminateReq });
        termRes.Ok.Should().BeTrue();

        var terminatedDto = JsonSerializer.Deserialize<UserActiveSessionResponseDto>(await termRes.TextAsync(), JsonOptions);
        terminatedDto.Should().NotBeNull();
        terminatedDto!.IsActive.Should().BeFalse();
        terminatedDto.TerminationReason.Should().Be(SessionTerminationReason.UserLogout.ToString());
    }

    [Fact]
    public async Task AdminDualControl_ChangeRequest_MakerChecker_And_ParameterUpdate_Flow_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var makerId = $"MAKER-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var checkerId = $"CHECKER-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        // 1. Maker submits administrative change request for BankGlobalParameters
        var paramPayload = new UpdateBankGlobalParametersRequest(
            BankName: "Sonar Bangla International Bank PLC",
            BaseCurrencyCode: "BDT",
            LocalCurrencyCode: "BDT",
            ChiefLocalOfficeBranchCode: "BR-001",
            WeeklyHolidaysCsv: "Friday,Saturday",
            FiscalYearStartMonth: 7,
            FiscalYearStartDay: 1,
            RealTimeBranchSettlementEnabled: true,
            AllowAlphaCustomerNumber: false,
            MaxExternalAccountNumberLength: 16,
            DailySpotRateListDetailsJson: null,
            UpdatedByUserId: makerId);

        var createReq = new CreateAdminChangeRequestDto(
            Category: AdminChangeCategory.BankGlobalParameters,
            ChangeDescription: "Update Bank Name to International branding",
            ChangePayloadJson: JsonSerializer.Serialize(paramPayload),
            MakerUserId: makerId,
            MakerBranchCode: "HO-001");

        var createRes = await api.PostAsync("/api/v1/admin/dual-control/requests", new() { DataObject = createReq });
        createRes.Ok.Should().BeTrue();
        createRes.Status.Should().Be(201);

        var createdDto = JsonSerializer.Deserialize<AdminChangeRequestResponseDto>(await createRes.TextAsync(), JsonOptions);
        createdDto.Should().NotBeNull();
        var requestId = createdDto!.RequestId;
        createdDto.Status.Should().Be(AdminChangeStatus.PendingCheckerApproval.ToString());

        // 2. Maker attempts to approve own request -> Must fail with AdminMakerCheckerInvariant
        var badApproveReq = new ApproveAdminChangeRequestDto(CheckerUserId: makerId, Remarks: "Self approving");
        var badApproveRes = await api.PostAsync($"/api/v1/admin/dual-control/requests/{requestId}/approve", new() { DataObject = badApproveReq });
        badApproveRes.Ok.Should().BeFalse();
        badApproveRes.Status.Should().Be(400);

        var badApproveText = await badApproveRes.TextAsync();
        badApproveText.Should().Contain("AdminMakerCheckerInvariant");

        // 3. Query Pending Requests
        var pendingRes = await api.GetAsync("/api/v1/admin/dual-control/pending");
        pendingRes.Ok.Should().BeTrue();
        var pendingList = JsonSerializer.Deserialize<List<AdminChangeRequestResponseDto>>(await pendingRes.TextAsync(), JsonOptions);
        pendingList.Should().NotBeNull();
        pendingList.Should().Contain(r => r.RequestId == requestId);

        // 4. Independent Checker approves request
        var goodApproveReq = new ApproveAdminChangeRequestDto(CheckerUserId: checkerId, Remarks: "Board resolution verified");
        var goodApproveRes = await api.PostAsync($"/api/v1/admin/dual-control/requests/{requestId}/approve", new() { DataObject = goodApproveReq });
        goodApproveRes.Ok.Should().BeTrue();

        var approvedDto = JsonSerializer.Deserialize<AdminChangeRequestResponseDto>(await goodApproveRes.TextAsync(), JsonOptions);
        approvedDto.Should().NotBeNull();
        approvedDto!.Status.Should().Be(AdminChangeStatus.Approved.ToString());
        approvedDto.CheckerUserId.Should().Be(checkerId);

        // 5. Query Bank Global Parameters -> Must reflect updated BankName
        var paramRes = await api.GetAsync("/api/v1/admin/parameters");
        paramRes.Ok.Should().BeTrue();

        var paramDto = JsonSerializer.Deserialize<BankGlobalParametersResponseDto>(await paramRes.TextAsync(), JsonOptions);
        paramDto.Should().NotBeNull();
        paramDto!.BankName.Should().Be("Sonar Bangla International Bank PLC");
        paramDto.BaseCurrencyCode.Should().Be("BDT");
    }
}
