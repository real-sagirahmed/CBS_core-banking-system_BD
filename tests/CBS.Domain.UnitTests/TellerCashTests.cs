using CBS.Domain.BranchCash;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class TellerCashTests
{
    [Fact]
    public void DenominationBundle_ValidDenominations_CalculatesTotalCorrectly()
    {
        // 10 notes of 1000 + 5 notes of 500 + 10 coins of 5
        // Total = 10*1000 + 5*500 + 10*5 = 10000 + 2500 + 50 = 12,550 BDT
        var bundle = CashDenominationBundle.Create(
            (1000, 10),
            (500, 5),
            (5, 10));

        bundle.TotalAmount.Should().Be(Money.Bdt(12550m));
        bundle.GetCount(1000).Should().Be(10);
        bundle.GetCount(500).Should().Be(5);
        bundle.GetCount(5).Should().Be(10);
        bundle.GetCount(100).Should().Be(0);
    }

    [Fact]
    public void DenominationBundle_InvalidDenomination_ThrowsInvariantViolation()
    {
        var action = () => CashDenominationBundle.Create((750, 2));

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*InvalidCurrencyDenominationInvariant*");
    }

    [Fact]
    public void DenominationBundle_NegativeQuantity_ThrowsInvariantViolation()
    {
        var action = () => CashDenominationBundle.Create((500, -1));

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*NegativeDenominationQuantityInvariant*");
    }

    [Fact]
    public void DenominationBundle_AddAndSubtract_MaintainsMathematicalAccuracy()
    {
        var initial = CashDenominationBundle.Create((1000, 5), (500, 4)); // 5000 + 2000 = 7000
        var deposit = CashDenominationBundle.Create((1000, 2), (100, 10)); // 2000 + 1000 = 3000

        var combined = initial.Add(deposit);
        combined.TotalAmount.Should().Be(Money.Bdt(10000m));
        combined.GetCount(1000).Should().Be(7);
        combined.GetCount(500).Should().Be(4);
        combined.GetCount(100).Should().Be(10);

        var withdrawal = CashDenominationBundle.Create((1000, 3), (100, 5)); // 3000 + 500 = 3500
        var remaining = combined.Subtract(withdrawal);
        remaining.TotalAmount.Should().Be(Money.Bdt(6500m));
        remaining.GetCount(1000).Should().Be(4);
        remaining.GetCount(100).Should().Be(5);
    }

    [Fact]
    public void DenominationBundle_SubtractMoreThanAvailable_ThrowsInsufficientQuantity()
    {
        var bundle = CashDenominationBundle.Create((1000, 2));
        var request = CashDenominationBundle.Create((1000, 3));

        var action = () => bundle.Subtract(request);

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*InsufficientDenominationQuantityInvariant*");
    }

    [Fact]
    public void TellerDrawer_Open_WithValidSupervisorAndDenominations_BecomesActive()
    {
        var drawer = new TellerDrawer(
            Guid.NewGuid(),
            "BR001",
            "CASHIER_01",
            new DateOnly(2026, 11, 9),
            Money.Bdt(500000m));

        var initialVaultBundle = CashDenominationBundle.Create((1000, 100), (500, 100)); // 150,000 BDT
        var initialAmount = Money.Bdt(150000m);

        drawer.Open(initialAmount, initialVaultBundle, "SUPERVISOR_01");

        drawer.Status.Should().Be(TellerDrawerStatus.Active);
        drawer.CurrentBalance.Should().Be(initialAmount);
        drawer.Denominations.TotalAmount.Should().Be(initialAmount);
        drawer.OpeningSupervisorUserId.Should().Be("SUPERVISOR_01");
    }

    [Fact]
    public void TellerDrawer_Open_WhenSupervisorEqualsCashier_ThrowsMakerCheckerSeparation()
    {
        var drawer = new TellerDrawer(
            Guid.NewGuid(),
            "BR001",
            "CASHIER_01",
            new DateOnly(2026, 11, 9),
            Money.Bdt(500000m));

        var bundle = CashDenominationBundle.Create((1000, 10));
        var action = () => drawer.Open(Money.Bdt(10000m), bundle, "CASHIER_01");

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*MakerCheckerSeparationInvariant*");
    }

    [Fact]
    public void TellerDrawer_Open_WhenAllocationAmountMismatchesDenominations_ThrowsMismatchInvariant()
    {
        var drawer = new TellerDrawer(
            Guid.NewGuid(),
            "BR001",
            "CASHIER_01",
            new DateOnly(2026, 11, 9),
            Money.Bdt(500000m));

        var bundle = CashDenominationBundle.Create((1000, 10)); // 10,000 BDT
        var action = () => drawer.Open(Money.Bdt(20000m), bundle, "SUPERVISOR_01");

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*VaultCashDenominationMismatchInvariant*");
    }

    [Fact]
    public void TellerDrawer_Deposit_WithinPostingLimit_SucceedsWithoutSupervisor()
    {
        var drawer = CreateActiveDrawer("CASHIER_01", "SUPERVISOR_01", 100000m, 500000m);

        var depositBundle = CashDenominationBundle.Create((1000, 50)); // 50,000 BDT
        drawer.ProcessCashDeposit(Money.Bdt(50000m), depositBundle);

        drawer.CurrentBalance.Should().Be(Money.Bdt(150000m));
    }

    [Fact]
    public void TellerDrawer_Deposit_ExceedingPostingLimit_WithoutSupervisor_ThrowsLimitExceededInvariant()
    {
        var drawer = CreateActiveDrawer("CASHIER_01", "SUPERVISOR_01", 100000m, 500000m);

        var depositBundle = CashDenominationBundle.Create((1000, 600)); // 600,000 BDT > 500,000 BDT limit
        var action = () => drawer.ProcessCashDeposit(Money.Bdt(600000m), depositBundle, supervisorApprovalId: null);

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*CashierPostingLimitExceededInvariant*");
    }

    [Fact]
    public void TellerDrawer_Deposit_ExceedingPostingLimit_WithSupervisor_Succeeds()
    {
        var drawer = CreateActiveDrawer("CASHIER_01", "SUPERVISOR_01", 100000m, 500000m);

        var depositBundle = CashDenominationBundle.Create((1000, 600)); // 600,000 BDT
        drawer.ProcessCashDeposit(Money.Bdt(600000m), depositBundle, supervisorApprovalId: "SUP_AUTH_9921");

        drawer.CurrentBalance.Should().Be(Money.Bdt(700000m));
    }

    [Fact]
    public void TellerDrawer_Withdrawal_WithinBalanceAndLimit_Succeeds()
    {
        var drawer = CreateActiveDrawer("CASHIER_01", "SUPERVISOR_01", 200000m, 500000m);

        var withdrawBundle = CashDenominationBundle.Create((1000, 50)); // 50,000 BDT
        drawer.ProcessCashWithdrawal(Money.Bdt(50000m), withdrawBundle);

        drawer.CurrentBalance.Should().Be(Money.Bdt(150000m));
    }

    [Fact]
    public void TellerDrawer_Withdrawal_ExceedingDrawerBalance_ThrowsInsufficientDrawerCash()
    {
        var drawer = CreateActiveDrawer("CASHIER_01", "SUPERVISOR_01", 50000m, 500000m);

        var withdrawBundle = CashDenominationBundle.Create((1000, 60)); // 60,000 BDT
        var action = () => drawer.ProcessCashWithdrawal(Money.Bdt(60000m), withdrawBundle);

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*InsufficientDrawerCashInvariant*");
    }

    [Fact]
    public void TellerDrawer_CloseAndBalance_WhenPhysicalMatchesSystem_SetsStatusBalanced()
    {
        var drawer = CreateActiveDrawer("CASHIER_01", "SUPERVISOR_01", 100000m, 500000m);

        // Physical count matches exactly 100,000 BDT
        var physical = CashDenominationBundle.Create((1000, 100));
        drawer.CloseAndBalanceDrawer(physical, "BRANCH_MGR_01", out decimal discrepancy);

        drawer.Status.Should().Be(TellerDrawerStatus.Balanced);
        discrepancy.Should().Be(0m);
        drawer.DiscrepancyAmount.Should().Be(0m);
    }

    [Fact]
    public void TellerDrawer_CloseAndBalance_WhenPhysicalShortage_SetsStatusDiscrepancySuspense()
    {
        var drawer = CreateActiveDrawer("CASHIER_01", "SUPERVISOR_01", 100000m, 500000m);

        // Physical count is 98,000 BDT (2,000 BDT cash shortage)
        var physical = CashDenominationBundle.Create((1000, 98));
        drawer.CloseAndBalanceDrawer(physical, "BRANCH_MGR_01", out decimal discrepancy);

        drawer.Status.Should().Be(TellerDrawerStatus.DiscrepancySuspense);
        discrepancy.Should().Be(-2000m);
        drawer.DiscrepancyAmount.Should().Be(-2000m);
    }

    private static TellerDrawer CreateActiveDrawer(
        string cashier,
        string supervisor,
        decimal initialAmount,
        decimal limit)
    {
        var drawer = new TellerDrawer(
            Guid.NewGuid(),
            "BR001",
            cashier,
            new DateOnly(2026, 11, 9),
            Money.Bdt(limit));

        int noteCount = (int)(initialAmount / 1000m);
        var bundle = CashDenominationBundle.Create((1000, noteCount));
        drawer.Open(Money.Bdt(initialAmount), bundle, supervisor);
        return drawer;
    }
}
