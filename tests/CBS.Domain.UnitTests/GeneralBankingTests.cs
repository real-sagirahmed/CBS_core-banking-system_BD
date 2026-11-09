using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.GeneralLedger;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.UnitTests;

public class GeneralBankingTests
{
    [Fact]
    public void BranchOperatingDay_InitiateEodBatch_WhenZeroPendingVouchers_ShouldSucceed()
    {
        var opDay = new BranchOperatingDay("001", new DateOnly(2025, 6, 30));
        Assert.Equal(DayStatus.Open, opDay.Status);

        opDay.InitiateEodBatch(pendingVouchersCount: 0);
        Assert.Equal(DayStatus.EodBatchRunning, opDay.Status);

        opDay.CompleteDayClose("EOD_BATCH_ADMIN");
        Assert.Equal(DayStatus.Closed, opDay.Status);
        Assert.Equal("EOD_BATCH_ADMIN", opDay.ClosedByUserId);
        Assert.NotNull(opDay.DayClosedAtUtc);
    }

    [Fact]
    public void BranchOperatingDay_InitiateEodBatch_WhenPendingVouchersExist_ShouldThrowHardGateInvariant()
    {
        // BRPD Circular 12/2024 Section 1.3.15-i: Hard gate blocking day close if pending vouchers exist
        var opDay = new BranchOperatingDay("001", new DateOnly(2025, 6, 30));

        var ex = Assert.Throws<InvariantViolationException>(() =>
            opDay.InitiateEodBatch(pendingVouchersCount: 3));

        Assert.Equal("PendingVoucherDayCloseBlockedInvariant", ex.InvariantName);
        Assert.Equal(DayStatus.Open, opDay.Status); // Status unchanged
    }

    [Fact]
    public void BranchOperatingDay_ExecuteBeginningOfDay_ShouldAdvanceDateAndOpenDay()
    {
        var opDay = new BranchOperatingDay("001", new DateOnly(2025, 6, 30));
        opDay.InitiateEodBatch(0);
        opDay.CompleteDayClose("ADMIN");

        var nextDate = new DateOnly(2025, 7, 1);
        opDay.ExecuteBeginningOfDay(nextDate);

        Assert.Equal(DayStatus.Open, opDay.Status);
        Assert.Equal(nextDate, opDay.CurrentBusinessDate);
        Assert.Null(opDay.DayClosedAtUtc);
    }

    [Fact]
    public void ExchangeRate_ConvertToBdt_ShouldCalculateAccurateBaseCurrencyAmount()
    {
        // BRPD Circular 12/2024 Section 1.3.2, 1.3.9: Multi-currency conversion
        var rate = new ExchangeRate(
            Currency.USD,
            midRate: 118.50m,
            buyRate: 118.00m,
            sellRate: 119.00m,
            rateDate: new DateOnly(2025, 6, 30));

        var usdAmount = Money.Usd(1000m);
        var bdtAmount = rate.ConvertToBdt(usdAmount);

        Assert.Equal(118500.00m, bdtAmount.Amount);
        Assert.Equal("BDT", bdtAmount.Currency.Code);
    }

    [Fact]
    public void ExchangeRate_CalculateRevaluationDelta_ShouldComputeAccurateMtmDifference()
    {
        // Original booked BDT: 115,000 BDT (booked at 115.00)
        // Current MTM rate: 118.50
        var rate = new ExchangeRate(
            Currency.USD,
            midRate: 118.50m,
            buyRate: 118.00m,
            sellRate: 119.00m,
            rateDate: new DateOnly(2025, 6, 30));

        var foreignHolding = Money.Usd(1000m);
        var originalBooked = Money.Bdt(115000.00m);

        var delta = rate.CalculateRevaluationDelta(foreignHolding, originalBooked);

        // MTM is 118,500 - 115,000 = +3,500 BDT Revaluation Gain
        Assert.Equal(3500.00m, delta.Amount);
    }

    [Fact]
    public void OrganizationalUnit_HierarchyCreation_ShouldSetProperAttributes()
    {
        // BRPD Circular 12/2024 Section 1.3.8: HO -> Division -> Region -> Cluster -> Branch
        var ho = new OrganizationalUnit("HO", "Head Office Dhaka", OrgUnitLevel.HeadOffice, "000000000");
        var branch = new OrganizationalUnit("001", "Principal Branch", OrgUnitLevel.Branch, "001260011", parentUnitCode: "HO");

        Assert.Equal(OrgUnitLevel.HeadOffice, ho.Level);
        Assert.Equal(OrgUnitLevel.Branch, branch.Level);
        Assert.Equal("HO", branch.ParentUnitCode);
        Assert.True(branch.IsActive);
    }
}
