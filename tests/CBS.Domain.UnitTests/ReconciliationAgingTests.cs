using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Reconciliation;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Automated unit tests locking Multi-Channel Reconciliation Aging
// and Statutory Provisioning invariants conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 1.4.1.
public class ReconciliationAgingTests
{
    private static readonly DateOnly BaseTxDate = new(2026, 1, 1);

    [Fact]
    public void ReconciliationAging_Under30Days_ZeroProvision()
    {
        var item = new ReconciliationAgingItem(
            Guid.NewGuid(),
            "InterBranch",
            "TR-IB-2026-001",
            "0101",
            "0102",
            Money.Bdt(100_000m),
            BaseTxDate,
            new DateOnly(2026, 1, 15)); // 14 days old

        item.AgeInDays.Should().Be(14);
        item.Bucket.Should().Be(ReconciliationAgingBucket.Days0To30);
        item.RequiredProvisionPercentage.Should().Be(0.0m);
        item.RequiredProvisionAmount.Amount.Should().Be(0m);
        item.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void ReconciliationAging_31To60Days_TenPercentProvision()
    {
        var item = new ReconciliationAgingItem(
            Guid.NewGuid(),
            "Nostro",
            "TR-NOSTRO-2026-002",
            "0101",
            "CITIBANK_NY",
            Money.Bdt(200_000m),
            BaseTxDate,
            new DateOnly(2026, 2, 15)); // 45 days old

        item.AgeInDays.Should().Be(45);
        item.Bucket.Should().Be(ReconciliationAgingBucket.Days31To60);
        item.RequiredProvisionPercentage.Should().Be(10.0m);
        item.RequiredProvisionAmount.Amount.Should().Be(20_000m); // 10% of 200,000
    }

    [Fact]
    public void ReconciliationAging_61To90Days_TwentyFivePercentProvision()
    {
        var item = new ReconciliationAgingItem(
            Guid.NewGuid(),
            "ATM",
            "TR-ATM-2026-003",
            "0101",
            "Q-CASH_SWITCH",
            Money.Bdt(50_000m),
            BaseTxDate,
            new DateOnly(2026, 3, 20)); // 78 days old

        item.AgeInDays.Should().Be(78);
        item.Bucket.Should().Be(ReconciliationAgingBucket.Days61To90);
        item.RequiredProvisionPercentage.Should().Be(25.0m);
        item.RequiredProvisionAmount.Amount.Should().Be(12_500m); // 25% of 50,000
    }

    [Fact]
    public void ReconciliationAging_91To180Days_FiftyPercentProvision()
    {
        var item = new ReconciliationAgingItem(
            Guid.NewGuid(),
            "RTGS",
            "TR-RTGS-2026-004",
            "0101",
            "BB_RTGS_SETTLEMENT",
            Money.Bdt(1_000_000m),
            BaseTxDate,
            new DateOnly(2026, 5, 1)); // 120 days old

        item.AgeInDays.Should().Be(120);
        item.Bucket.Should().Be(ReconciliationAgingBucket.Days91To180);
        item.RequiredProvisionPercentage.Should().Be(50.0m);
        item.RequiredProvisionAmount.Amount.Should().Be(500_000m); // 50% of 1,000,000
    }

    [Fact]
    public void ReconciliationAging_Over180Days_HundredPercentProvision()
    {
        var item = new ReconciliationAgingItem(
            Guid.NewGuid(),
            "BEFTN",
            "TR-BEFTN-2026-005",
            "0101",
            "SONALI_BANK",
            Money.Bdt(300_000m),
            BaseTxDate,
            new DateOnly(2026, 8, 1)); // 212 days old

        item.AgeInDays.Should().Be(212);
        item.Bucket.Should().Be(ReconciliationAgingBucket.Over180Days);
        item.RequiredProvisionPercentage.Should().Be(100.0m);
        item.RequiredProvisionAmount.Amount.Should().Be(300_000m); // 100% of 300,000
    }

    [Fact]
    public void ReconciliationAging_ResolveItem_ClearsProvisionAndSetsResolvedDate()
    {
        var item = new ReconciliationAgingItem(
            Guid.NewGuid(),
            "InterBranch",
            "TR-IB-2026-006",
            "0101",
            "0105",
            Money.Bdt(80_000m),
            BaseTxDate,
            new DateOnly(2026, 3, 1)); // 59 days old -> 10% provision (8,000 BDT)

        item.RequiredProvisionAmount.Amount.Should().Be(8_000m);

        // Act: Resolve item with rectification voucher
        item.MarkResolved("RECT-VOUCHER-2026-99", new DateOnly(2026, 3, 5));

        // Assert
        item.IsResolved.Should().BeTrue();
        item.ResolutionReference.Should().Be("RECT-VOUCHER-2026-99");
        item.ResolvedDate.Should().Be(new DateOnly(2026, 3, 5));
        item.RequiredProvisionAmount.Amount.Should().Be(0m);
        item.RequiredProvisionPercentage.Should().Be(0m);
    }

    [Fact]
    public void ReconciliationAging_InvalidAmount_ThrowsInvariantViolation()
    {
        var act = () => new ReconciliationAgingItem(
            Guid.NewGuid(),
            "InterBranch",
            "TR-IB-2026-007",
            "0101",
            "0102",
            Money.Bdt(0m),
            BaseTxDate,
            new DateOnly(2026, 1, 5));

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*amount must be strictly positive*");
    }
}
