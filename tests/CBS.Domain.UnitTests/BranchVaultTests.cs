using CBS.Domain.BranchCash;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class BranchVaultTests
{
    private const string BranchCode = "BR001";
    private const string Custodian = "HEAD_CASHIER_01";
    private const string JointCustodian = "BRANCH_MGR_01";

    [Fact]
    public void BranchVault_Initialization_SetsPropertiesAndStatusCorrectly()
    {
        var initialAmount = Money.Bdt(5000000m); // 50 Lac BDT
        var insuredLimit = Money.Bdt(10000000m); // 1 Crore BDT
        var bundle = CashDenominationBundle.Create((1000, 5000)); // 5,000,000 BDT

        var vault = new BranchVault(
            Guid.NewGuid(),
            BranchCode,
            Custodian,
            JointCustodian,
            initialAmount,
            insuredLimit,
            bundle);

        vault.BranchCode.Should().Be(BranchCode);
        vault.VaultCustodianUserId.Should().Be(Custodian);
        vault.JointCustodianUserId.Should().Be(JointCustodian);
        vault.CurrentBalance.Should().Be(initialAmount);
        vault.InsuredCashLimit.Should().Be(insuredLimit);
        vault.IsInsuredLimitExceeded().Should().BeFalse();
        vault.GetExcessOverInsuredLimit().Should().Be(0m);
        vault.IsVaultLocked.Should().BeFalse();
        vault.EveningBankingSessionActive.Should().BeFalse();
    }

    [Fact]
    public void BranchVault_WhenCustodianEqualsJointCustodian_ThrowsDualCustodySeparation()
    {
        var initialAmount = Money.Bdt(100000m);
        var insuredLimit = Money.Bdt(1000000m);
        var bundle = CashDenominationBundle.Create((1000, 100));

        var act = () => new BranchVault(
            Guid.NewGuid(),
            BranchCode,
            Custodian,
            Custodian, // Same custodian
            initialAmount,
            insuredLimit,
            bundle);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*DualCustodySeparationInvariant*");
    }

    [Fact]
    public void BranchVault_WhenDenominationBundleMismatchesInitialBalance_ThrowsMismatchInvariant()
    {
        var initialAmount = Money.Bdt(200000m);
        var insuredLimit = Money.Bdt(1000000m);
        var bundle = CashDenominationBundle.Create((1000, 100)); // 100,000 != 200,000

        var act = () => new BranchVault(
            Guid.NewGuid(),
            BranchCode,
            Custodian,
            JointCustodian,
            initialAmount,
            insuredLimit,
            bundle);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*VaultCashDenominationMismatchInvariant*");
    }

    [Fact]
    public void BranchVault_DepositFromExternalChest_IncreasesBalanceAndDenominations()
    {
        var vault = CreateTestVault(5000000m, 10000000m);
        var depositAmount = Money.Bdt(2000000m);
        var depositBundle = CashDenominationBundle.Create((1000, 2000));

        vault.DepositCashFromExternalChest(depositAmount, depositBundle, "BB-CHEST-REF-001", Custodian, JointCustodian);

        vault.CurrentBalance.Should().Be(Money.Bdt(7000000m));
        vault.Denominations.GetCount(1000).Should().Be(7000);
    }

    [Fact]
    public void BranchVault_DepositFromChest_WhenDenominationMismatchesAmount_ThrowsMismatch()
    {
        var vault = CreateTestVault(5000000m, 10000000m);
        var depositAmount = Money.Bdt(2000000m);
        var depositBundle = CashDenominationBundle.Create((1000, 1500)); // 1.5M != 2.0M

        var act = () => vault.DepositCashFromExternalChest(depositAmount, depositBundle, "BB-CHEST-REF-001", Custodian, JointCustodian);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*ChestCashDenominationMismatchInvariant*");
    }

    [Fact]
    public void BranchVault_RemitToExternalChest_ExceedingVaultBalance_ThrowsInsufficientVaultCash()
    {
        var vault = CreateTestVault(1000000m, 5000000m);
        var remitAmount = Money.Bdt(2000000m);
        var remitBundle = CashDenominationBundle.Create((1000, 2000));

        var act = () => vault.RemitCashToExternalChest(remitAmount, remitBundle, "BB-REMIT-001", Custodian, JointCustodian);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*InsufficientVaultCashInvariant*");
    }

    [Fact]
    public void BranchVault_AllocateToTeller_WhenTellerEqualsCustodian_ThrowsMakerCheckerSeparation()
    {
        var vault = CreateTestVault(5000000m, 10000000m);
        var allocAmount = Money.Bdt(500000m);
        var allocBundle = CashDenominationBundle.Create((1000, 500));

        var act = () => vault.AllocateCashToTeller(allocAmount, allocBundle, Custodian, Custodian);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*MakerCheckerSeparationInvariant*");
    }

    [Fact]
    public void BranchVault_AllocateToTeller_ValidRequest_DecreasesVaultBalance()
    {
        var vault = CreateTestVault(5000000m, 10000000m);
        var allocAmount = Money.Bdt(500000m);
        var allocBundle = CashDenominationBundle.Create((1000, 500));

        vault.AllocateCashToTeller(allocAmount, allocBundle, "CASHIER_01", Custodian);

        vault.CurrentBalance.Should().Be(Money.Bdt(4500000m));
        vault.Denominations.GetCount(1000).Should().Be(4500);
    }

    [Fact]
    public void BranchVault_ReceiveFromTeller_IncreasesVaultBalance()
    {
        var vault = CreateTestVault(5000000m, 10000000m);
        var returnAmount = Money.Bdt(300000m);
        var returnBundle = CashDenominationBundle.Create((1000, 300));

        vault.ReceiveCashFromTeller(returnAmount, returnBundle, "CASHIER_01", Custodian);

        vault.CurrentBalance.Should().Be(Money.Bdt(5300000m));
        vault.Denominations.GetCount(1000).Should().Be(5300);
    }

    [Fact]
    public void BranchVault_InsuredLimit_ReportsExceededWhenBalanceOverLimit()
    {
        var vault = CreateTestVault(4000000m, 5000000m); // 4M balance, 5M limit
        vault.IsInsuredLimitExceeded().Should().BeFalse();

        // Deposit 2M -> total 6M > 5M limit
        vault.DepositCashFromExternalChest(Money.Bdt(2000000m), CashDenominationBundle.Create((1000, 2000)), "BB-CHEST-99", Custodian, JointCustodian);

        vault.IsInsuredLimitExceeded().Should().BeTrue();
        vault.GetExcessOverInsuredLimit().Should().Be(1000000m);
    }

    [Fact]
    public void BranchVault_EveningBanking_OpenAndClose_TogglesActiveState()
    {
        var vault = CreateTestVault(1000000m, 5000000m);
        vault.EveningBankingSessionActive.Should().BeFalse();

        vault.OpenEveningBankingSession("OFFICER_EVENING_01");
        vault.EveningBankingSessionActive.Should().BeTrue();
        vault.EveningSessionOpenedAtUtc.Should().NotBeNull();

        vault.CloseEveningBankingSession("OFFICER_EVENING_01");
        vault.EveningBankingSessionActive.Should().BeFalse();
        vault.EveningSessionOpenedAtUtc.Should().BeNull();
    }

    [Fact]
    public void BranchVault_WhenLocked_RejectsCashOperations()
    {
        var vault = CreateTestVault(1000000m, 5000000m);
        vault.LockVault(Custodian, JointCustodian);
        vault.IsVaultLocked.Should().BeTrue();

        var act = () => vault.AllocateCashToTeller(Money.Bdt(100000m), CashDenominationBundle.Create((1000, 100)), "CASHIER_01", Custodian);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*VaultLockedInvariant*");
    }

    private static BranchVault CreateTestVault(decimal initialAmount, decimal insuredLimit)
    {
        int noteCount = (int)(initialAmount / 1000m);
        var bundle = CashDenominationBundle.Create((1000, noteCount));

        return new BranchVault(
            Guid.NewGuid(),
            BranchCode,
            Custodian,
            JointCustodian,
            Money.Bdt(initialAmount),
            Money.Bdt(insuredLimit),
            bundle);
    }
}
