using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Shariah;
using CBS.Domain.Shariah.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class ShariahComplianceTests
{
    [Fact]
    public void MudarabaInvestmentPool_ShouldDistributeProfit_WithMudaribSplitAndPerIrrDeductions()
    {
        // Arrange: 35% Mudarib, 2% PER, 1% IRR
        var pool = new MudarabaInvestmentPool(
            "Mudaraba General Pool",
            MudarabaPoolType.GeneralInvestmentPool,
            "BDT",
            35.0m,
            2.0m,
            1.0m);

        pool.AddDeposit(10000000m);
        pool.SetWeightage(new MudarabaWeightage("MSB", "Mudaraba Savings", 1.00m, 5.50m));
        pool.SetWeightage(new MudarabaWeightage("MTDR-12", "Mudaraba Term Deposit 1-Year", 1.35m, 7.50m));

        decimal grossIncome = 1000000m;
        var asOfDate = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = pool.DistributeProfit(grossIncome, asOfDate);

        // Assert:
        // Bank (Mudarib) Share = 1,000,000 * 35% = 350,000
        // Rab-ul-Mal Gross = 1,000,000 - 350,000 = 650,000
        // PER (2% of 650,000) = 13,000
        // IRR (1% of 650,000) = 6,500
        // Net Distributable = 650,000 - 13,000 - 6,500 = 630,500
        result.MudaribShareAmount.Should().Be(350000m);
        result.RabUlMalGrossShare.Should().Be(650000m);
        result.PerDeductionAmount.Should().Be(13000m);
        result.IrrDeductionAmount.Should().Be(6500m);
        result.NetDistributableProfit.Should().Be(630500m);

        pool.AccumulatedPerBalance.Should().Be(13000m);
        pool.AccumulatedIrrBalance.Should().Be(6500m);
        pool.LastDistributionDateUtc.Should().Be(asOfDate);
    }

    [Fact]
    public void MudarabaInvestmentPool_ShouldThrowException_WhenGrossIncomeIsZeroOrNegative()
    {
        // Arrange
        var pool = new MudarabaInvestmentPool(
            "Pool-01",
            MudarabaPoolType.SpecialInvestmentPool,
            "BDT",
            40.0m);

        // Act
        var act = () => pool.DistributeProfit(0m, DateTime.UtcNow);

        // Assert
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Gross investment pool income must be strictly greater than zero*");
    }

    [Fact]
    public void AlWadiahAccount_ShouldEnforceZeroInterestRate_AndCapitalGuarantee()
    {
        // Arrange
        var account = new AlWadiahAccount(
            "1011234567890",
            "CIF-ALW-001",
            "BDT",
            "CONTRACT-AMANAH-2026-001");

        // Act & Assert
        account.InterestRate.Should().Be(0.00m);
        account.IsActive.Should().BeTrue();
        account.Balance.Should().Be(0m);

        account.Deposit(250000m);
        account.Balance.Should().Be(250000m);

        account.Withdraw(50000m);
        account.Balance.Should().Be(200000m);

        // Withdrawal exceeding balance is rejected
        var overdraw = () => account.Withdraw(300000m);
        overdraw.Should().Throw<InvariantViolationException>()
            .WithMessage("*Insufficient funds*");
    }

    [Fact]
    public void AlWadiahAccount_ShouldRejectClosure_WhenBalanceNotZero()
    {
        // Arrange
        var account = new AlWadiahAccount("1011234567891", "CIF-002", "BDT", "AMANAH-002");
        account.Deposit(15000m);

        // Act
        var act = () => account.CloseAccount();

        // Assert
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Cannot close Al-Wadiah account with remaining balance*");
    }

    [Fact]
    public void MurabahaFacility_ShouldRejectDisbursement_WhenGoodsNotInspected()
    {
        // Arrange
        var facility = new MurabahaFinancingFacility(
            "MUR-2026-0001",
            "CIF-CORP-01",
            "0101",
            5000000m,
            500000m,
            24,
            "100 Metric Tons of Industrial Raw Cotton");

        facility.Status.Should().Be(ShariahFacilityStatus.Draft);

        var vendorOrder = new VendorDisbursementOrder(
            "Meghna Textile Mills Ltd",
            "1019876543210",
            "085270115",
            "INV-2026-9042",
            5000000m,
            "INSP-PROOF-001",
            DateTime.UtcNow);

        // Act
        var act = () => facility.DisburseToVendor(vendorOrder, "MGR-001");

        // Assert
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Goods must be physically inspected prior to vendor payment*");
    }

    [Fact]
    public void MurabahaFacility_ShouldDisburseToVendor_WhenGoodsInspected_AndInvoiceMatches()
    {
        // Arrange
        var facility = new MurabahaFinancingFacility(
            "MUR-2026-0002",
            "CIF-CORP-02",
            "0101",
            3000000m,
            360000m,
            12,
            "Commercial Fleet 2x Cargo Trucks");

        facility.RecordGoodsInspection("INSP-PROOF-VEHICLE-002", "INSPECTOR-01");
        facility.Status.Should().Be(ShariahFacilityStatus.GoodsInspected);

        var vendorOrder = new VendorDisbursementOrder(
            "Tata Motors Bangladesh Dealer Ltd",
            "1015555444433",
            "085270115",
            "INV-TRUCK-8899",
            3000000m,
            "INSP-PROOF-VEHICLE-002",
            DateTime.UtcNow);

        // Act
        facility.DisburseToVendor(vendorOrder, "MGR-001");

        // Assert
        facility.Status.Should().Be(ShariahFacilityStatus.DisbursedToVendor);
        facility.OutstandingBalance.Should().Be(3360000m); // Total Sale Price = Cost + Profit
        facility.DisbursedAtUtc.Should().NotBeNull();
        facility.VendorDisbursementOrderJson.Should().Contain("Tata Motors");

        // Test Repayment
        facility.RecordRepayment(3360000m);
        facility.Status.Should().Be(ShariahFacilityStatus.Settled);
        facility.OutstandingBalance.Should().Be(0m);
    }

    [Fact]
    public void MurabahaFacility_ShouldRejectDisbursement_WhenInvoiceAmountMismatchesPurchaseCost()
    {
        // Arrange
        var facility = new MurabahaFinancingFacility(
            "MUR-2026-0003",
            "CIF-CORP-03",
            "0101",
            2000000m,
            200000m,
            12,
            "Printing Machinery");

        facility.RecordGoodsInspection("INSP-003", "INSP-01");

        var mismatchedOrder = new VendorDisbursementOrder(
            "Equipment Supplier Ltd",
            "1019999888877",
            "085270115",
            "INV-777",
            2500000m, // Mismatched invoice amount
            "INSP-003",
            DateTime.UtcNow);

        // Act
        var act = () => facility.DisburseToVendor(mismatchedOrder, "MGR-001");

        // Assert
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Vendor invoice amount 2500000.00 does not match contract purchase cost 2000000.00*");
    }

    [Fact]
    public void QuardFacility_ShouldEnforceZeroInterestAndMarkupInvariant()
    {
        // Arrange
        var facility = new QuardFinancingFacility(
            "QRD-2026-0001",
            "CIF-IND-01",
            50000m,
            500m, // Documented actual admin fee
            6);

        // Assert
        facility.InterestRate.Should().Be(0.00m);
        facility.ProfitRate.Should().Be(0.00m);
        facility.OutstandingBalance.Should().Be(50000m);
        facility.IsSettled.Should().BeFalse();

        // Act
        facility.RecordRepayment(50000m);

        // Assert
        facility.OutstandingBalance.Should().Be(0m);
        facility.IsSettled.Should().BeTrue();
    }

    [Fact]
    public void ShariahTaWidhCharityFund_ShouldEnforceSuspenseAndCharityDisbursement()
    {
        // Arrange
        var record = new ShariahTaWidhCharityFund(
            "MUR-2026-0002",
            "CIF-CORP-02",
            15000m);

        record.Status.Should().Be(TaWidhStatus.AccruedInSuspense);
        record.OverduePenaltyAmount.Should().Be(15000m);

        // Act - Attempt disbursement without Shariah Board Approval
        var prematureAct = () => record.DisburseToWelfare("OFFICER-01");
        prematureAct.Should().Throw<InvariantViolationException>()
            .WithMessage("*cannot be disbursed without prior Shariah Supervisory Board approval*");

        // Approve and Disburse
        record.ApproveByShariahBoard("Donation of medical equipment to Dhaka Shishu Hospital");
        record.Status.Should().Be(TaWidhStatus.ApprovedByShariahBoard);

        record.DisburseToWelfare("COMPLIANCE-HEAD-01");
        record.Status.Should().Be(TaWidhStatus.DisbursedToCharity);
        record.DisbursedAtUtc.Should().NotBeNull();
        record.DisbursedByOfficerId.Should().Be("COMPLIANCE-HEAD-01");
    }
}
