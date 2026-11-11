using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Security.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Security.ValueObjects;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class SystemAdminAndGovernancePlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public SystemAdminAndGovernancePlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Test_AdminProposal_MakerCheckerDualControl_ApproveActivatesUser()
    {
        var api = await _fixture.CreateApiContextAsync();
        string userId = $"USR{Random.Shared.Next(100000, 999999)}";
        string empId = $"EMP{Random.Shared.Next(10000, 99999)}";

        // 1. Maker proposes user creation
        var proposeReq = new ProposeCreateUserRequest(
            userId,
            empId,
            "Tariqul Islam",
            $"{userId.ToLowerInvariant()}@cbs.bank.bd",
            "0101",
            UserGroupType.Teller,
            new List<string> { "BranchTeller" },
            "InitialPass@123",
            DoaLimitProfile.DefaultTeller("0101"),
            "MAKER01");

        var proposeRes = await api.PostAsync("/api/v1/admin/users/propose", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = proposeReq
        });
        proposeRes.Status.Should().Be(201);
        var proposal = (await proposeRes.JsonAsync())?.Deserialize<AdminProposalResponse>(JsonOptions)!;
        proposal.Status.Should().Be("PendingApproval");

        // 2. Maker attempts to approve own proposal -> Must fail (BRPD 10.3.5-x & 1.3.18)
        var selfApproveRes = await api.PostAsync($"/api/v1/admin/proposals/{proposal.Id}/approve", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = new ApproveProposalRequest("MAKER01", "Attempting self approval")
        });
        selfApproveRes.Status.Should().Be(400);

        // 3. Checker approves proposal -> Must succeed and activate user profile
        var checkerApproveRes = await api.PostAsync($"/api/v1/admin/proposals/{proposal.Id}/approve", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new ApproveProposalRequest("CHECKER01", "Approved after KYC and background check")
        });
        checkerApproveRes.Status.Should().Be(200);
        var approvedProposal = (await checkerApproveRes.JsonAsync())?.Deserialize<AdminProposalResponse>(JsonOptions)!;
        approvedProposal.Status.Should().Be("Approved");

        // 4. Verify user exists and is active
        var userRes = await api.GetAsync($"/api/v1/admin/users/{userId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "AUDITOR01" }
        });
        userRes.Status.Should().Be(200);
        var user = (await userRes.JsonAsync())?.Deserialize<SystemUserResponse>(JsonOptions)!;
        user.UserId.Should().Be(userId);
        user.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Test_UserLogin_EnforcesFirstLoginPasswordChange_AndPreventsHistoryReuse()
    {
        var api = await _fixture.CreateApiContextAsync();
        string userId = $"USR{Random.Shared.Next(100000, 999999)}";
        string empId = $"EMP{Random.Shared.Next(10000, 99999)}";
        string initialPass = "InitialSecret@123";

        // Create user
        var proposeReq = new ProposeCreateUserRequest(
            userId,
            empId,
            "Anwarul Haque",
            $"{userId.ToLowerInvariant()}@cbs.bank.bd",
            "0101",
            UserGroupType.Teller,
            new List<string> { "BranchTeller" },
            initialPass,
            DoaLimitProfile.DefaultTeller("0101"),
            "MAKER01");

        var proposeRes = await api.PostAsync("/api/v1/admin/users/propose", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = proposeReq
        });
        var proposal = (await proposeRes.JsonAsync())?.Deserialize<AdminProposalResponse>(JsonOptions)!;

        await api.PostAsync($"/api/v1/admin/proposals/{proposal.Id}/approve", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new ApproveProposalRequest("CHECKER01", "Approved")
        });

        // 1. Initial Login: MustChangePasswordOnNextLogin is true (BRPD 10.1.3-iv)
        var loginRes = await api.PostAsync("/api/v1/admin/auth/login", new APIRequestContextOptions
        {
            DataObject = new LoginRequest(userId, initialPass, "192.168.1.105")
        });
        loginRes.Status.Should().Be(200);
        var loginData = (await loginRes.JsonAsync())?.Deserialize<LoginResponse>(JsonOptions)!;
        loginData.MustChangePasswordOnNextLogin.Should().BeTrue();
        loginData.ActiveSessionToken.Should().NotBeNullOrWhiteSpace();

        // 2. Change Password to same initial password -> Must fail (BRPD 10.1.3-v)
        var reuseRes = await api.PostAsync("/api/v1/admin/auth/change-password", new APIRequestContextOptions
        {
            DataObject = new ChangePasswordRequest(userId, initialPass, initialPass)
        });
        reuseRes.Status.Should().Be(400);

        // 3. Change Password to valid new password
        string newPass = "BrandNewSecret@2024";
        var changeRes = await api.PostAsync("/api/v1/admin/auth/change-password", new APIRequestContextOptions
        {
            DataObject = new ChangePasswordRequest(userId, initialPass, newPass)
        });
        changeRes.Status.Should().Be(200);

        // 4. Second Login: MustChangePasswordOnNextLogin is false, telemetry contains previous attempt
        var login2Res = await api.PostAsync("/api/v1/admin/auth/login", new APIRequestContextOptions
        {
            DataObject = new LoginRequest(userId, newPass, "192.168.1.106")
        });
        login2Res.Status.Should().Be(200);
        var loginData2 = (await login2Res.JsonAsync())?.Deserialize<LoginResponse>(JsonOptions)!;
        loginData2.MustChangePasswordOnNextLogin.Should().BeFalse();
        loginData2.PreviousSuccessfulLoginIp.Should().Be("192.168.1.105");
    }

    [Fact]
    public async Task Test_SuperAdmin_FinancialTransactionBlocked_With403Forbidden()
    {
        var api = await _fixture.CreateApiContextAsync();
        string adminUserId = $"ADMIN{Random.Shared.Next(1000, 9999)}";
        string empId = $"EMP{Random.Shared.Next(10000, 99999)}";

        // Create Administrator user
        var proposeReq = new ProposeCreateUserRequest(
            adminUserId,
            empId,
            "Security SuperAdmin",
            $"{adminUserId.ToLowerInvariant()}@cbs.bank.bd",
            "0101",
            UserGroupType.Administrator,
            new List<string> { "SystemAdmin" },
            "SuperAdminPass@2024",
            null,
            "MAKER01");

        var proposeRes = await api.PostAsync("/api/v1/admin/users/propose", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = proposeReq
        });
        var proposal = (await proposeRes.JsonAsync())?.Deserialize<AdminProposalResponse>(JsonOptions)!;

        await api.PostAsync($"/api/v1/admin/proposals/{proposal.Id}/approve", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new ApproveProposalRequest("CHECKER01", "Approved SuperAdmin")
        });

        // Attempting financial operation (Opening account) using Administrator identity must be rejected (BRPD 10.3.5-vii)
        var openAccRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>
            {
                ["X-User-Id"] = adminUserId,
                ["X-User-Role"] = "SystemAdmin"
            },
            DataObject = new OpenDepositAccountRequest(
                "CIF123456",
                DepositProductType.SavingsAccount,
                "0101",
                1000m,
                true)
        });

        openAccRes.Status.Should().Be(403);
    }

    [Fact]
    public async Task Test_DoaLimit_EvaluationAndSupervisorOverride()
    {
        var api = await _fixture.CreateApiContextAsync();
        string tellerId = $"TELLER{Random.Shared.Next(1000, 9999)}";
        string empId = $"EMP{Random.Shared.Next(10000, 99999)}";

        // Create Teller user
        var proposeReq = new ProposeCreateUserRequest(
            tellerId,
            empId,
            "Mahmudul Hasan",
            $"{tellerId.ToLowerInvariant()}@cbs.bank.bd",
            "0101",
            UserGroupType.Teller,
            new List<string> { "BranchTeller" },
            "TellerPass@123",
            DoaLimitProfile.DefaultTeller("0101"),
            "MAKER01");

        var proposeRes = await api.PostAsync("/api/v1/admin/users/propose", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = proposeReq
        });
        var proposal = (await proposeRes.JsonAsync())?.Deserialize<AdminProposalResponse>(JsonOptions)!;

        await api.PostAsync($"/api/v1/admin/proposals/{proposal.Id}/approve", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new ApproveProposalRequest("CHECKER01", "Approved")
        });

        // 1. Transaction within DOA limit (50,000 BDT <= 100,000 BDT) -> Allowed
        var eval1Res = await api.PostAsync("/api/v1/admin/doa/evaluate", new APIRequestContextOptions
        {
            DataObject = new EvaluateDoaRequest(tellerId, 50_000m, false, false, false, "TXN-001", "0101201000010")
        });
        eval1Res.Status.Should().Be(200);
        var eval1 = (await eval1Res.JsonAsync())?.Deserialize<EvaluateDoaResponse>(JsonOptions)!;
        eval1.IsAllowed.Should().BeTrue();
        eval1.RequiresOverride.Should().BeFalse();

        // 2. Transaction exceeding DOA limit (250,000 BDT > 100,000 BDT) -> Requires Override
        var eval2Res = await api.PostAsync("/api/v1/admin/doa/evaluate", new APIRequestContextOptions
        {
            DataObject = new EvaluateDoaRequest(tellerId, 250_000m, false, false, false, "TXN-002", "0101201000010")
        });
        eval2Res.Status.Should().Be(200);
        var eval2 = (await eval2Res.JsonAsync())?.Deserialize<EvaluateDoaResponse>(JsonOptions)!;
        eval2.IsAllowed.Should().BeFalse();
        eval2.RequiresOverride.Should().BeTrue();
        eval2.OverrideId.Should().NotBeNull();

        // Create Supervisor user
        string supId = $"SUP{Random.Shared.Next(1000, 9999)}";
        string supEmpId = $"EMP{Random.Shared.Next(10000, 99999)}";
        var supProposeReq = new ProposeCreateUserRequest(
            supId,
            supEmpId,
            "Supervisor Khan",
            $"{supId.ToLowerInvariant()}@cbs.bank.bd",
            "0101",
            UserGroupType.Supervisor,
            new List<string> { "BranchSupervisor" },
            "SupervisorPass@123",
            DoaLimitProfile.DefaultSupervisor("0101"),
            "MAKER01");

        var supProposeRes = await api.PostAsync("/api/v1/admin/users/propose", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = supProposeReq
        });
        var supProposal = (await supProposeRes.JsonAsync())?.Deserialize<AdminProposalResponse>(JsonOptions)!;
        await api.PostAsync($"/api/v1/admin/proposals/{supProposal.Id}/approve", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new ApproveProposalRequest("CHECKER01", "Approved Supervisor")
        });

        // 3. Supervisor authorizes override
        var authRes = await api.PostAsync($"/api/v1/admin/doa/overrides/{eval2.OverrideId!.Value}/authorize", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = supId },
            DataObject = new AuthorizeOverrideRequest(supId, "Authorized by Branch Manager for VIP customer")
        });
        authRes.Status.Should().Be(200);
        var overrideItem = (await authRes.JsonAsync())?.Deserialize<DoaOverrideResponse>(JsonOptions)!;
        overrideItem.Status.Should().Be("Authorized");
        overrideItem.SupervisorUserId.Should().Be(supId);
    }

    [Fact]
    public async Task Test_AuditJournal_PreservesAdministrativeActivities()
    {
        var api = await _fixture.CreateApiContextAsync();
        string userId = $"JOURNAL_USR_{Random.Shared.Next(1000, 9999)}";

        // Propose user to generate audit journal entry
        var proposeReq = new ProposeCreateUserRequest(
            userId,
            $"EMP_{Random.Shared.Next(1000, 9999)}",
            "Journal Audit User",
            $"{userId.ToLowerInvariant()}@cbs.bank.bd",
            "0101",
            UserGroupType.Teller,
            new List<string> { "BranchTeller" },
            "TempSecret@123",
            null,
            "MAKER01");

        await api.PostAsync("/api/v1/admin/users/propose", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = proposeReq
        });

        // Query UserProfileActivity audit journals
        var journalRes = await api.GetAsync("/api/v1/admin/audit/journals?category=UserProfileActivity", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "AUDITOR01" }
        });
        journalRes.Status.Should().Be(200);
        var journals = (await journalRes.JsonAsync())?.Deserialize<List<AuditJournalResponse>>(JsonOptions)!;
        journals.Should().NotBeNull();
        journals.Should().Contain(j => j.Action == "ProposeCreateUser" && j.EntityId == userId);
    }
}
