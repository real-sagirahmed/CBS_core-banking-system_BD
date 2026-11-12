// ARCHITECTURAL INTENT: Comprehensive Domain Unit Tests locking Treasury Management,
// FX Blotter, Net Open Position (NOP), Dealer & Counterparty Limits, Mark-to-Market (MTM) Revaluation,
// Nostro Mirror Reconciliation with statutory debit aging provisions, and Statutory Reserves (CRR/SLR)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 6, DOS Circular No. 05/2008, and MPD Circular No. 03/2020.

using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Treasury;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class TreasuryManagementAndReserveTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void FxBlotterPosition_Should_Accurately_Aggregate_Bought_Sold_And_Weighted_Average_Rates()
    {
        // ARCHITECTURAL INTENT: Real-time FX Blotter aggregations must strictly track
        // weighted average buy and sell rates and net open position per currency [BRPD 6.1.1, 6.1.2]
        var blotter = new FxBlotterPosition(
            id: Guid.NewGuid(),
            currency: "USD",
            positionDate: Today,
            openingPosition: 100_000m,
            initialMidRate: 117.50m);

        // Buy 500,000 @ 117.50
        blotter.RecordBuy(500_000m, 117.50m);
        // Buy 300,000 @ 118.00
        blotter.RecordBuy(300_000m, 118.00m);

        // Sell 400,000 @ 118.20
        blotter.RecordSell(400_000m, 118.20m);

        blotter.Currency.Should().Be("USD");
        blotter.TotalBoughtFcy.Should().Be(800_000m);
        blotter.TotalSoldFcy.Should().Be(400_000m);
        // Net: 100,000 (open) + 800,000 - 400,000 = 500,000
        blotter.NetPosition.Should().Be(500_000m);

        // Weighted Average Buy Rate: ((500,000 * 117.50) + (300,000 * 118.00)) / 800,000
        // (58,750,000 + 35,400,000) / 800,000 = 94,150,000 / 800,000 = 117.6875
        blotter.WeightedAverageBuyRate.Should().Be(117.6875m);
        blotter.WeightedAverageSellRate.Should().Be(118.20m);

        // Revaluation mid rate 117.80 -> BDT equivalent = 500,000 * 117.80 = 58,900,000
        blotter.UpdateMidRate(117.80m);
        blotter.BdtEquivalent.Should().Be(58_900_000m);
    }

    [Fact]
    public void TreasuryLimitProfile_DealerDaylightLimit_Should_Allow_Within_Sanction_And_Reject_Breach()
    {
        // ARCHITECTURAL INTENT: Dealer Daylight and Overnight limits must strictly block trades
        // exceeding approved thresholds per BRPD Circular 12 Module 6.2.2-iii
        var limit = new TreasuryLimitProfile(
            id: Guid.NewGuid(),
            limitType: TreasuryLimitType.DealerDaylight,
            identifier: "DEALER-001",
            currency: "USD",
            sanctionedLimit: 2_000_000m,
            approvedByUserId: "HEAD-OF-TREASURY",
            effectiveDate: Today.AddMonths(-1),
            expiryDate: Today.AddYears(1)
        );

        limit.AvailableAmount.Should().Be(2_000_000m);

        // Trade 1: 1,200,000 USD -> Allowed
        limit.CheckAndUtilize(1_200_000m, "DEAL-USD-001");
        limit.UtilizedAmount.Should().Be(1_200_000m);
        limit.AvailableAmount.Should().Be(800_000m);

        // Trade 2: 900,000 USD -> Exceeds remaining 800,000 USD -> Must throw InvariantViolationException
        var act = () => limit.CheckAndUtilize(900_000m, "DEAL-USD-002");
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*breaches sanctioned limit*");
    }

    [Fact]
    public void TreasuryLimitProfile_CounterpartyCreditLine_Should_Enforce_Ceiling()
    {
        // ARCHITECTURAL INTENT: Counterparty credit lines prevent systemic credit exposure
        // to single bank counterparties [BRPD 6.3.1-e, 6.3.1-g]
        var line = new TreasuryLimitProfile(
            id: Guid.NewGuid(),
            limitType: TreasuryLimitType.CounterpartyCreditLine,
            identifier: "EBL-BD-DHAKA",
            currency: "BDT",
            sanctionedLimit: 500_000_000m,
            approvedByUserId: "CHIEF-RISK-OFFICER",
            effectiveDate: Today.AddDays(-10),
            expiryDate: Today.AddDays(355)
        );

        line.AvailableAmount.Should().Be(500_000_000m);
        line.CheckAndUtilize(400_000_000m, "CALL-LOAN-01");
        line.AvailableAmount.Should().Be(100_000_000m);

        // Breaching utilization
        var act = () => line.CheckAndUtilize(150_000_000m, "CALL-LOAN-02");
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*breaches sanctioned limit*");

        // Release 200,000,000 on settlement
        line.Release(200_000_000m);
        line.UtilizedAmount.Should().Be(200_000_000m);
        line.AvailableAmount.Should().Be(300_000_000m);
    }

    [Fact]
    public void MtmRevaluationBatch_Should_Accurately_Compute_Unrealized_Gains_And_Losses()
    {
        // ARCHITECTURAL INTENT: MTM revaluation records mark-to-market differences into
        // revaluation reserve accounts [BRPD 6.3.1-v, DOS 05/2008]
        var batch = new MtmRevaluationBatch(
            id: Guid.NewGuid(),
            batchReference: "MTM-20261112-001",
            revaluationDate: Today,
            totalForwardDealsEvaluated: 1,
            totalSecuritiesEvaluated: 1,
            totalUnrealizedGainsBdt: 150_000m,
            totalUnrealizedLossesBdt: 50_000m,
            executedByUserId: "TREASURY-ANALYST-01",
            remarks: "Daily EOD MTM Batch"
        );

        batch.TotalForwardDealsEvaluated.Should().Be(1);
        batch.TotalSecuritiesEvaluated.Should().Be(1);
        batch.TotalUnrealizedGainsBdt.Should().Be(150_000m);
        batch.TotalUnrealizedLossesBdt.Should().Be(50_000m);
        batch.NetRevaluationReserveBdt.Should().Be(100_000m);
        batch.Status.Should().Be("Completed");
    }

    [Fact]
    public void NostroMirrorReconciliationItem_Exact_Match_Should_Reconcile_Item()
    {
        // ARCHITECTURAL INTENT: 1:1 automated matching on reference, value date, and amount [BRPD 6.4.2]
        var item = new NostroMirrorReconciliationItem(
            id: Guid.NewGuid(),
            nostroAccountCode: "NOSTRO-USD-CITI-NY",
            currency: "USD",
            valueDate: Today.AddDays(-5),
            entryType: "Debit",
            localBookReference: "FT-OUT-2026-9901",
            localBookAmount: 250_000.00m
        );

        item.Status.Should().Be(NostroReconciliationStatus.Unmatched);

        item.MatchWithStatement(
            statementReference: "FT-OUT-2026-9901",
            statementAmount: 250_000.00m,
            matchRule: "ExactReferenceAndAmount",
            matchedByUserId: "NOSTRO-OFFICER-01"
        );

        item.Status.Should().Be(NostroReconciliationStatus.MatchedAuto);
        item.StatementReference.Should().Be("FT-OUT-2026-9901");
        item.StatementAmount.Should().Be(250_000.00m);
        item.MatchedByUserId.Should().Be("NOSTRO-OFFICER-01");
    }

    [Theory]
    [InlineData(30, 0.00, 0.00)]
    [InlineData(89, 0.00, 0.00)]
    [InlineData(90, 20.00, 20_000.00)]
    [InlineData(120, 20.00, 20_000.00)]
    [InlineData(180, 50.00, 50_000.00)]
    [InlineData(181, 50.00, 50_000.00)]
    [InlineData(300, 50.00, 50_000.00)]
    [InlineData(365, 100.00, 100_000.00)]
    [InlineData(400, 100.00, 100_000.00)]
    public void NostroMirrorReconciliationItem_Unreconciled_Debit_Should_Calculate_Statutory_Provision(
        int daysAgo,
        decimal expectedRate,
        decimal expectedProvision)
    {
        // ARCHITECTURAL INTENT: Statutory Provisioning for un-reconciled debit items:
        // <90 days: 0%, 90-179 days: 20%, 180-364 days: 50%, >=365 days: 100% [BRPD 6.4.2]
        var item = new NostroMirrorReconciliationItem(
            id: Guid.NewGuid(),
            nostroAccountCode: "NOSTRO-USD-JPMC-NY",
            currency: "USD",
            valueDate: Today.AddDays(-daysAgo),
            entryType: "Debit",
            localBookReference: $"DEBIT-{daysAgo}",
            localBookAmount: 100_000.00m
        );

        item.UpdateAgingAndProvision(Today);

        item.DaysOutstanding.Should().Be(daysAgo);
        item.RequiredProvisionRate.Should().Be(expectedRate);
        item.RequiredProvisionAmount.Should().Be(expectedProvision);
    }

    [Fact]
    public void NostroMirrorReconciliationItem_Unreconciled_Credit_Should_Have_Zero_Provision()
    {
        // ARCHITECTURAL INTENT: Un-reconciled credit items do NOT attract statutory provisioning [BRPD 6.4.2]
        var item = new NostroMirrorReconciliationItem(
            id: Guid.NewGuid(),
            nostroAccountCode: "NOSTRO-EUR-COMMERZ",
            currency: "EUR",
            valueDate: Today.AddDays(-400),
            entryType: "Credit",
            localBookReference: "CREDIT-OLD",
            localBookAmount: 500_000.00m
        );

        item.UpdateAgingAndProvision(Today);

        item.DaysOutstanding.Should().Be(400);
        item.RequiredProvisionRate.Should().Be(0m);
        item.RequiredProvisionAmount.Should().Be(0m);
    }

    [Fact]
    public void StatutoryReserveAssessment_Conventional_Should_Enforce_CRR_350_And_SLR_1300()
    {
        // ARCHITECTURAL INTENT: Conventional Banks CRR: Daily minimum 3.50%, bi-weekly 4.00%; SLR: 13.00%
        // per Bangladesh Bank MPD Circular No. 03/2020 & BRPD 12 Module 6.3.1-l
        var tdtl = 10_000_000_000m; // 10,000 Crore BDT

        // Compliant scenario: CRR cash = 360 Cr (3.60%), SLR assets = 1350 Cr (13.50%)
        var assessment = new StatutoryReserveAssessment(
            id: Guid.NewGuid(),
            assessmentDate: Today,
            bankingType: BankingType.Conventional,
            tdtlBaseBdt: tdtl,
            maintainedCrrCashBdt: 360_000_000m,
            maintainedSlrAssetsBdt: 1_350_000_000m,
            evaluatedByUserId: "RISK-OFFICER-01"
        );

        assessment.RequiredCrrDailyBdt.Should().Be(350_000_000m); // 3.50%
        assessment.RequiredCrrBiWeeklyBdt.Should().Be(400_000_000m); // 4.00%
        assessment.IsDailyCrrCompliant.Should().BeTrue();
        assessment.DailyCrrShortfallBdt.Should().Be(0m);

        assessment.RequiredSlrBdt.Should().Be(1_300_000_000m); // 13.00%
        assessment.IsSlrCompliant.Should().BeTrue();
        assessment.SlrShortfallBdt.Should().Be(0m);
    }

    [Fact]
    public void StatutoryReserveAssessment_Conventional_Shortfall_Should_Be_Calculated()
    {
        // ARCHITECTURAL INTENT: CRR and SLR shortfalls must be accurately flagged for BB regulatory penalty reporting
        var tdtl = 10_000_000_000m;

        // Deficit: CRR cash = 320 Cr (shortfall 30 Cr against 3.50%), SLR = 1200 Cr (shortfall 100 Cr against 13.00%)
        var assessment = new StatutoryReserveAssessment(
            id: Guid.NewGuid(),
            assessmentDate: Today,
            bankingType: BankingType.Conventional,
            tdtlBaseBdt: tdtl,
            maintainedCrrCashBdt: 320_000_000m,
            maintainedSlrAssetsBdt: 1_200_000_000m,
            evaluatedByUserId: "RISK-OFFICER-01"
        );

        assessment.IsDailyCrrCompliant.Should().BeFalse();
        assessment.DailyCrrShortfallBdt.Should().Be(30_000_000m);

        assessment.IsSlrCompliant.Should().BeFalse();
        assessment.SlrShortfallBdt.Should().Be(100_000_000m);
    }

    [Fact]
    public void StatutoryReserveAssessment_Islamic_Should_Enforce_SLR_550()
    {
        // ARCHITECTURAL INTENT: Islamic Banks SLR requirement is 5.50% of TDTL [MPD Circular 03/2020]
        var tdtl = 5_000_000_000m; // 5,000 Crore BDT

        var assessment = new StatutoryReserveAssessment(
            id: Guid.NewGuid(),
            assessmentDate: Today,
            bankingType: BankingType.Islamic,
            tdtlBaseBdt: tdtl,
            maintainedCrrCashBdt: 200_000_000m, // 4.00%
            maintainedSlrAssetsBdt: 280_000_000m, // 5.60% (above 5.50%)
            evaluatedByUserId: "ISLAMIC-RISK-OFFICER"
        );

        assessment.RequiredSlrBdt.Should().Be(275_000_000m); // 5.50% of 5,000,000,000 = 275,000,000
        assessment.IsSlrCompliant.Should().BeTrue();
        assessment.SlrShortfallBdt.Should().Be(0m);
    }
}
