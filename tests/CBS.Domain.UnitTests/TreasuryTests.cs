using CBS.Domain.Enums;
using CBS.Domain.Treasury;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class TreasuryTests
{
    [Fact]
    public void TreasuryDeal_WhenValueDateBeforeDealDate_ShouldThrowViolation()
    {
        var dealDate = new DateTime(2026, 6, 10);
        var invalidValueDate = new DateTime(2026, 6, 9); // Earlier than deal date!

        var act = () => TreasuryDeal.CaptureByFrontOffice(
            dealReference: "TR-FX-2026-0001",
            type: TreasuryDealType.FxSpot,
            direction: DealDirection.Buy,
            counterpartyBankBic: "SCBLBDDX",
            counterpartyName: "Standard Chartered Bank Bangladesh",
            primaryAmount: new Money(1000000m, Currency.USD),
            secondaryAmount: new Money(120000000m, Currency.BDT),
            exchangeRate: 120.00m,
            interestRate: 0m,
            dealDate: dealDate,
            valueDate: invalidValueDate,
            frontOfficeDealerUserId: "DEALER_01");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Value date cannot be earlier than deal trade date*");
    }

    [Fact]
    public void TreasuryDeal_MidOfficeVerification_WhenDealerVerifiesOwnDeal_ShouldThrowViolation()
    {
        var deal = TreasuryDeal.CaptureByFrontOffice(
            dealReference: "TR-FX-2026-0001",
            type: TreasuryDealType.FxSpot,
            direction: DealDirection.Buy,
            counterpartyBankBic: "SCBLBDDX",
            counterpartyName: "Standard Chartered Bank Bangladesh",
            primaryAmount: new Money(1000000m, Currency.USD),
            secondaryAmount: new Money(120000000m, Currency.BDT),
            exchangeRate: 120.00m,
            interestRate: 0m,
            dealDate: new DateTime(2026, 6, 10),
            valueDate: new DateTime(2026, 6, 12),
            frontOfficeDealerUserId: "DEALER_01");

        // Act: Front office dealer attempting mid-office verification
        var act = () => deal.VerifyByMidOffice("DEALER_01");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Front-office dealer cannot perform mid-office risk and limit verification*");
    }

    [Fact]
    public void TreasuryDeal_BackOfficeSettlement_WhenDealNotVerifiedByMidOffice_ShouldThrowViolation()
    {
        var deal = TreasuryDeal.CaptureByFrontOffice(
            dealReference: "TR-FX-2026-0001",
            type: TreasuryDealType.FxSpot,
            direction: DealDirection.Buy,
            counterpartyBankBic: "SCBLBDDX",
            counterpartyName: "Standard Chartered Bank Bangladesh",
            primaryAmount: new Money(1000000m, Currency.USD),
            secondaryAmount: new Money(120000000m, Currency.BDT),
            exchangeRate: 120.00m,
            interestRate: 0m,
            dealDate: new DateTime(2026, 6, 10),
            valueDate: new DateTime(2026, 6, 12),
            frontOfficeDealerUserId: "DEALER_01");

        // Act: Settling directly from back office without mid-office verification
        var act = () => deal.SettleByBackOffice("SETTLEMENT_OFFICER_01");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Settlement is prohibited before mid-office risk and limit verification*");
    }

    [Fact]
    public void TreasuryDeal_BackOfficeSettlement_WhenDealerOrMidOfficeSettles_ShouldThrowViolation()
    {
        var deal = TreasuryDeal.CaptureByFrontOffice(
            dealReference: "TR-FX-2026-0001",
            type: TreasuryDealType.FxSpot,
            direction: DealDirection.Buy,
            counterpartyBankBic: "SCBLBDDX",
            counterpartyName: "Standard Chartered Bank Bangladesh",
            primaryAmount: new Money(1000000m, Currency.USD),
            secondaryAmount: new Money(120000000m, Currency.BDT),
            exchangeRate: 120.00m,
            interestRate: 0m,
            dealDate: new DateTime(2026, 6, 10),
            valueDate: new DateTime(2026, 6, 12),
            frontOfficeDealerUserId: "DEALER_01");

        deal.VerifyByMidOffice("MID_OFFICE_01");

        // Act 1: Dealer attempting back-office settlement
        var actDealer = () => deal.SettleByBackOffice("DEALER_01");
        actDealer.Should().Throw<InvalidOperationException>()
            .WithMessage("*Front-office dealer strictly prohibited from executing back-office settlement*");

        // Act 2: Mid-office risk officer attempting back-office settlement
        var actMidOffice = () => deal.SettleByBackOffice("MID_OFFICE_01");
        actMidOffice.Should().Throw<InvalidOperationException>()
            .WithMessage("*Mid-office risk officer strictly prohibited from executing back-office settlement*");
    }

    [Fact]
    public void TreasuryDeal_ValidThreeTierLifecycle_ShouldTransitionToSettled()
    {
        var deal = TreasuryDeal.CaptureByFrontOffice(
            dealReference: "TR-MM-2026-0001",
            type: TreasuryDealType.CallMoneyBorrowing,
            direction: DealDirection.Borrow,
            counterpartyBankBic: "EBLBBDDH",
            counterpartyName: "Eastern Bank PLC",
            primaryAmount: new Money(50000000m, Currency.BDT),
            secondaryAmount: new Money(50000000m, Currency.BDT),
            exchangeRate: 1.00m,
            interestRate: 9.50m,
            dealDate: new DateTime(2026, 6, 10),
            valueDate: new DateTime(2026, 6, 11),
            frontOfficeDealerUserId: "FRONT_DEALER_A");

        deal.Status.Should().Be(TreasuryDealStatus.CapturedByFrontOffice);

        deal.VerifyByMidOffice("MID_RISK_OFFICER_B");
        deal.Status.Should().Be(TreasuryDealStatus.VerifiedByMidOffice);

        deal.SettleByBackOffice("BACK_SETTLEMENT_C");
        deal.Status.Should().Be(TreasuryDealStatus.SettledByBackOffice);
        deal.SettledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void CrrSlrPosition_Conventional_WhenCrrBelow3Point5Percent_ShouldDetectCrrShortfall()
    {
        // NDTL = 10,000,000,000 BDT (1,000 Crore)
        // 3.50% daily minimum = 350,000,000 BDT (35 Crore)
        // Maintained = 300,000,000 BDT (30 Crore) -> 50,000,000 BDT shortfall!
        var ndtl = new Money(10000000000m, Currency.BDT);
        var maintainedCrr = new Money(300000000m, Currency.BDT);
        var maintainedSlr = new Money(1500000000m, Currency.BDT); // 15% > 13%

        var pos = CrrSlrPosition.Calculate(ndtl, BankingType.Conventional, maintainedCrr, maintainedSlr);

        pos.HasCrrShortfall.Should().BeTrue();
        pos.ActualCrrPercentage.Should().Be(3.00m);
        pos.CrrShortfallOrSurplusAmount.Amount.Should().Be(-50000000m);

        pos.HasSlrShortfall.Should().BeFalse();
        pos.ActualSlrPercentage.Should().Be(15.00m);
    }

    [Fact]
    public void CrrSlrPosition_Conventional_WhenSlrBelow13Percent_ShouldDetectSlrShortfall()
    {
        // NDTL = 10,000,000,000 BDT
        // 13.00% SLR requirement = 1,300,000,000 BDT
        // Maintained SLR = 1,100,000,000 BDT (11.00%) -> Shortfall of 200,000,000 BDT!
        var ndtl = new Money(10000000000m, Currency.BDT);
        var maintainedCrr = new Money(450000000m, Currency.BDT); // 4.5% > 3.5%
        var maintainedSlr = new Money(1100000000m, Currency.BDT);

        var pos = CrrSlrPosition.Calculate(ndtl, BankingType.Conventional, maintainedCrr, maintainedSlr);

        pos.HasCrrShortfall.Should().BeFalse();
        pos.HasSlrShortfall.Should().BeTrue();
        pos.ActualSlrPercentage.Should().Be(11.00m);
        pos.SlrShortfallOrSurplusAmount.Amount.Should().Be(-200000000m);
    }

    [Fact]
    public void CrrSlrPosition_Islamic_WhenSlrMeets5Point5Percent_ShouldShowNoShortfall()
    {
        // For Islamic banks, SLR is 5.50% (instead of 13.00%)
        // NDTL = 10,000,000,000 BDT -> Required SLR = 550,000,000 BDT
        // Maintained SLR = 600,000,000 BDT (6.00%)
        var ndtl = new Money(10000000000m, Currency.BDT);
        var maintainedCrr = new Money(400000000m, Currency.BDT);
        var maintainedSlr = new Money(600000000m, Currency.BDT);

        var pos = CrrSlrPosition.Calculate(ndtl, BankingType.Islamic, maintainedCrr, maintainedSlr);

        pos.HasCrrShortfall.Should().BeFalse();
        pos.HasSlrShortfall.Should().BeFalse();
        pos.SlrRequiredRate.Should().Be(5.50m);
        pos.ActualSlrPercentage.Should().Be(6.00m);
    }

    [Fact]
    public void LiquidityGapProfile_WhenShortTermDeficitExceeds10Percent_ShouldTriggerPrudentialAlert()
    {
        // 1-14 days bucket: Inflow 500M, Outflow 800M -> Net gap -300M (Deficit ratio = 300 / 800 = 37.5% > 10%)
        var bucketData = new List<(LiquidityBucket, decimal, decimal)>
        {
            (LiquidityBucket.Days1To14, 500000000m, 800000000m),
            (LiquidityBucket.Days15To28, 400000000m, 350000000m),
            (LiquidityBucket.Days29To90, 600000000m, 500000000m)
        };

        var profile = LiquidityGapProfile.Evaluate("BDT", bucketData);

        profile.HasShortTermDeficitAlert.Should().BeTrue();
        profile.Summary.Should().Contain("Prudential Alert: 1-14 days structural liquidity deficit");
        profile.Buckets.First().NetGap.Should().Be(-300000000m);
    }

    [Fact]
    public void LiquidityGapProfile_WhenProfileWithinTolerance_ShouldNotTriggerAlert()
    {
        // 1-14 days bucket: Inflow 750M, Outflow 800M -> Net gap -50M (Deficit ratio = 50 / 800 = 6.25% <= 10%)
        var bucketData = new List<(LiquidityBucket, decimal, decimal)>
        {
            (LiquidityBucket.Days1To14, 750000000m, 800000000m),
            (LiquidityBucket.Days15To28, 500000000m, 400000000m),
            (LiquidityBucket.Days29To90, 600000000m, 500000000m)
        };

        var profile = LiquidityGapProfile.Evaluate("BDT", bucketData);

        profile.HasShortTermDeficitAlert.Should().BeFalse();
        profile.Summary.Should().Contain("Normal liquidity profile within tolerance");
    }
}
