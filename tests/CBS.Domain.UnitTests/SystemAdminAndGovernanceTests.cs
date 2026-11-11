using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Security;
using CBS.Domain.Security.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class SystemAdminAndGovernanceTests
{
    [Fact]
    public void Test_Password_MustChangeOnFirstLogin_AndCannotReuseLast4Passwords()
    {
        var user = new SystemUserProfile(
            "TELLER01",
            "EMP1001",
            "Jamil Ahmed",
            "jamil@cbs.bank.bd",
            "0101",
            UserGroupType.Teller,
            new List<string> { "BranchTeller" },
            "TempPass@123",
            null,
            "ADMIN01",
            DateTime.UtcNow);

        user.MustChangePasswordOnNextLogin.Should().BeTrue();

        // 1. Cannot reuse active initial password
        var reuseAction = () => user.ChangePassword("TempPass@123", "TempPass@123", DateTime.UtcNow);
        reuseAction.Should().Throw<InvariantViolationException>()
            .WithMessage("*cannot be identical to the current active password*");

        // 2. Rotate 4 times
        user.ChangePassword("TempPass@123", "NewSecret@2024", DateTime.UtcNow);
        user.MustChangePasswordOnNextLogin.Should().BeFalse();

        user.ChangePassword("NewSecret@2024", "SecretTwo@2024", DateTime.UtcNow);
        user.ChangePassword("SecretTwo@2024", "SecretThree@2024", DateTime.UtcNow);
        user.ChangePassword("SecretThree@2024", "SecretFour@2024", DateTime.UtcNow);

        // 3. Attempting to reuse any of the last 4 passwords must throw
        var historyReuseAction = () => user.ChangePassword("SecretFour@2024", "NewSecret@2024", DateTime.UtcNow);
        historyReuseAction.Should().Throw<InvariantViolationException>()
            .WithMessage("*cannot match any of the last 4 previous passwords*");
    }

    [Fact]
    public void Test_Password_ExpiresAfter30Days()
    {
        var now = DateTime.UtcNow;
        var user = new SystemUserProfile(
            "TELLER02",
            "EMP1002",
            "Nadia Islam",
            "nadia@cbs.bank.bd",
            "0101",
            UserGroupType.Teller,
            new List<string> { "BranchTeller" },
            "SecretPass@123",
            null,
            "ADMIN01",
            now);

        user.IsPasswordExpired(now.AddDays(15)).Should().BeFalse();
        user.IsPasswordExpired(now.AddDays(30)).Should().BeTrue();
        user.IsPasswordExpired(now.AddDays(45)).Should().BeTrue();
    }

    [Fact]
    public void Test_SuperAdmin_FinancialTransactionProhibition_ThrowsInvariant()
    {
        var admin = new SystemUserProfile(
            "SYSADMIN01",
            "EMP9999",
            "Root Administrator",
            "admin@cbs.bank.bd",
            "0101",
            UserGroupType.Administrator,
            new List<string> { "SystemAdmin" },
            "AdminSecret@2024",
            null,
            "ROOT",
            DateTime.UtcNow);

        // Administrator limits must be strictly zero
        admin.DoaLimits.MaxLocalDebitAmount.Should().Be(0m);
        admin.DoaLimits.MaxLocalCreditAmount.Should().Be(0m);

        // Financial transaction check must throw InvariantViolationException
        var txnAction = () => admin.AssertCanPerformFinancialTransaction();
        txnAction.Should().Throw<InvariantViolationException>()
            .WithMessage("*strictly forbidden from executing or approving financial transactions*");

        // Attempting to assign non-zero DOA limits to Administrator must throw
        var updateLimitsAction = () => admin.UpdateDoaLimits(DoaLimitProfile.DefaultTeller("0101"));
        updateLimitsAction.Should().Throw<InvariantViolationException>()
            .WithMessage("*Administrator group cannot be assigned non-zero financial transaction limits*");
    }

    [Fact]
    public void Test_AdminProposal_MakerCheckerDualControl_EnforcesSeparation()
    {
        var proposal = new AdminActionProposal(
            Guid.NewGuid(),
            AdminActionType.CreateUser,
            "NEWUSER01",
            "{}",
            "MAKER01",
            DateTime.UtcNow);

        proposal.Status.Should().Be(AdminProposalStatus.PendingApproval);

        // Maker cannot approve own proposal
        var makerApprove = () => proposal.Approve("MAKER01", "Self approval", DateTime.UtcNow);
        makerApprove.Should().Throw<InvariantViolationException>()
            .WithMessage("*cannot approve their own administrative proposal*");

        // Checker approval succeeds
        proposal.Approve("CHECKER01", "Verified by Operations Manager", DateTime.UtcNow);
        proposal.Status.Should().Be(AdminProposalStatus.Approved);
        proposal.CheckerUserId.Should().Be("CHECKER01");
    }

    [Fact]
    public void Test_DoaLimits_Evaluation_IdentifiesLimitBreachesAccurately()
    {
        var limits = DoaLimitProfile.DefaultTeller("0101");

        // Local Debit: limit is 100,000 BDT
        var (allowed1, _, _) = limits.EvaluateTransaction(50_000m, isCredit: false, isInterBranch: false, isGl: false);
        allowed1.Should().BeTrue();

        var (allowed2, userLimit2, reason2) = limits.EvaluateTransaction(150_000m, isCredit: false, isInterBranch: false, isGl: false);
        allowed2.Should().BeFalse();
        userLimit2.Should().Be(100_000m);
        reason2.Should().Contain("exceeds user limit of 100,000.00 BDT");

        // GL Posting: teller has 0 BDT GL posting limit
        var (allowedGl, userLimitGl, reasonGl) = limits.EvaluateTransaction(10_000m, isCredit: false, isInterBranch: false, isGl: true);
        allowedGl.Should().BeFalse();
        userLimitGl.Should().Be(0m);
        reasonGl.Should().Contain("exceeds user GL posting limit of 0.00 BDT");
    }

    [Fact]
    public void Test_LoginTelemetry_EnforcesSingleSession_AndLocksAfter5FailedAttempts()
    {
        var user = new SystemUserProfile(
            "TELLER03",
            "EMP1003",
            "Sultan Mahmud",
            "sultan@cbs.bank.bd",
            "0101",
            UserGroupType.Teller,
            new List<string> { "BranchTeller" },
            "SecretPass@123",
            null,
            "ADMIN01",
            DateTime.UtcNow);

        // 4 failed attempts
        for (int i = 0; i < 4; i++)
        {
            user.RecordLoginAttempt(false, "192.168.1.50", DateTime.UtcNow, "Wrong password");
            user.Status.Should().Be(UserAccountStatus.Active);
        }

        // 5th failed attempt locks the account
        user.RecordLoginAttempt(false, "192.168.1.50", DateTime.UtcNow, "Wrong password");
        user.Status.Should().Be(UserAccountStatus.Locked);

        // Successful login generates session token and preserves previous telemetry
        var activeUser = new SystemUserProfile(
            "TELLER04",
            "EMP1004",
            "Fatema Begum",
            "fatema@cbs.bank.bd",
            "0101",
            UserGroupType.Teller,
            new List<string> { "BranchTeller" },
            "SecretPass@123",
            null,
            "ADMIN01",
            DateTime.UtcNow);

        var time1 = DateTime.UtcNow.AddHours(-1);
        string session1 = activeUser.RecordLoginAttempt(true, "10.0.0.1", time1);
        session1.Should().NotBeNullOrWhiteSpace();

        var time2 = DateTime.UtcNow;
        string session2 = activeUser.RecordLoginAttempt(true, "10.0.0.2", time2);
        session2.Should().NotBeNullOrWhiteSpace();
        session2.Should().NotBe(session1); // Invalidate previous session

        activeUser.PreviousSuccessfulLoginAtUtc.Should().Be(time1);
        activeUser.PreviousSuccessfulLoginIp.Should().Be("10.0.0.1");
        activeUser.LastLoginAtUtc.Should().Be(time2);
        activeUser.LastLoginWorkstationIp.Should().Be("10.0.0.2");
    }
}
