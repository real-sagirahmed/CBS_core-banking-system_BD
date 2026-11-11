using CBS.Domain.BranchCash;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class TellerCashLimitAndSwapTests
{
    [Fact]
    public void TellerDrawer_Deposit_ExceedingCashHoldingLimit_WithoutSupervisor_ThrowsHoldingLimitExceeded()
    {
        // Drawer with 300,000 BDT, PostingLimit 500,000 BDT, HoldingLimit 500,000 BDT
        var drawer = new TellerDrawer(
            Guid.NewGuid(),
            "BR001",
            "CASHIER_01",
            new DateOnly(2026, 11, 11),
            Money.Bdt(500000m),
            Money.Bdt(500000m));

        drawer.Open(Money.Bdt(300000m), CashDenominationBundle.Create((1000, 300)), "SUPERVISOR_01");

        // Deposit 250,000 BDT (<= PostingLimit 500k, but balance becomes 550k > HoldingLimit 500k)
        var depositBundle = CashDenominationBundle.Create((1000, 250));
        var act = () => drawer.ProcessCashDeposit(Money.Bdt(250000m), depositBundle, supervisorApprovalId: null);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*TellerCashHoldingLimitExceededInvariant*");
    }

    [Fact]
    public void TellerDrawer_Deposit_ExceedingCashHoldingLimit_WithSupervisor_Succeeds()
    {
        var drawer = new TellerDrawer(
            Guid.NewGuid(),
            "BR001",
            "CASHIER_01",
            new DateOnly(2026, 11, 11),
            Money.Bdt(500000m),
            Money.Bdt(500000m));

        drawer.Open(Money.Bdt(300000m), CashDenominationBundle.Create((1000, 300)), "SUPERVISOR_01");

        var depositBundle = CashDenominationBundle.Create((1000, 250));
        drawer.ProcessCashDeposit(Money.Bdt(250000m), depositBundle, supervisorApprovalId: "SUP_OVERRIDE_441");

        drawer.CurrentBalance.Should().Be(Money.Bdt(550000m));
    }

    [Fact]
    public void TellerDrawer_SwapDenominations_BundleParity_ExchangesDenominationsWithZeroDelta()
    {
        var drawer = new TellerDrawer(
            Guid.NewGuid(),
            "BR001",
            "CASHIER_01",
            new DateOnly(2026, 11, 11),
            Money.Bdt(500000m));

        // Initial: 100 notes of 1000 = 100,000 BDT
        drawer.Open(Money.Bdt(100000m), CashDenominationBundle.Create((1000, 100)), "SUPERVISOR_01");

        // Swap: Give 10 notes of 1000 (10,000 BDT) and receive 100 notes of 100 (10,000 BDT)
        var givenBundle = CashDenominationBundle.Create((1000, 10));
        var receivedBundle = CashDenominationBundle.Create((100, 100));

        drawer.SwapDenominations(givenBundle, receivedBundle, "SUPERVISOR_01");

        drawer.CurrentBalance.Should().Be(Money.Bdt(100000m)); // Net balance invariant
        drawer.Denominations.GetCount(1000).Should().Be(90); // 100 - 10
        drawer.Denominations.GetCount(100).Should().Be(100);  // 0 + 100
        drawer.Denominations.TotalAmount.Should().Be(Money.Bdt(100000m));
    }

    [Fact]
    public void TellerDrawer_SwapDenominations_MismatchBundleValue_ThrowsMismatchInvariant()
    {
        var drawer = new TellerDrawer(
            Guid.NewGuid(),
            "BR001",
            "CASHIER_01",
            new DateOnly(2026, 11, 11),
            Money.Bdt(500000m));

        drawer.Open(Money.Bdt(100000m), CashDenominationBundle.Create((1000, 100)), "SUPERVISOR_01");

        var givenBundle = CashDenominationBundle.Create((1000, 10)); // 10,000 BDT
        var receivedBundle = CashDenominationBundle.Create((100, 90)); // 9,000 BDT != 10,000 BDT

        var act = () => drawer.SwapDenominations(givenBundle, receivedBundle, "SUPERVISOR_01");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*DenominationSwapValueMismatchInvariant*");
    }

    [Fact]
    public void TellerDrawer_SwapDenominations_WithoutSupervisor_ThrowsSupervisorAuthorizationRequired()
    {
        var drawer = new TellerDrawer(
            Guid.NewGuid(),
            "BR001",
            "CASHIER_01",
            new DateOnly(2026, 11, 11),
            Money.Bdt(500000m));

        drawer.Open(Money.Bdt(100000m), CashDenominationBundle.Create((1000, 100)), "SUPERVISOR_01");

        var givenBundle = CashDenominationBundle.Create((1000, 10));
        var receivedBundle = CashDenominationBundle.Create((100, 100));

        var act = () => drawer.SwapDenominations(givenBundle, receivedBundle, supervisorApprovalId: "");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*SupervisorAuthorizationRequiredInvariant*");
    }

    [Fact]
    public void TellerDrawer_SwapDenominations_InsufficientNoteQuantity_ThrowsInsufficientQuantity()
    {
        var drawer = new TellerDrawer(
            Guid.NewGuid(),
            "BR001",
            "CASHIER_01",
            new DateOnly(2026, 11, 11),
            Money.Bdt(500000m));

        // Initial: 5 notes of 1000 = 5,000 BDT
        drawer.Open(Money.Bdt(5000m), CashDenominationBundle.Create((1000, 5)), "SUPERVISOR_01");

        // Try to give 10 notes of 1000
        var givenBundle = CashDenominationBundle.Create((1000, 10));
        var receivedBundle = CashDenominationBundle.Create((500, 20));

        var act = () => drawer.SwapDenominations(givenBundle, receivedBundle, "SUPERVISOR_01");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*InsufficientDenominationQuantityInvariant*");
    }
}
