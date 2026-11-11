using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Lending.Demand;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Automated unit tests locking Demand Loan invariants
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.5 & 5.1.4.
public class DemandLoanTests
{
    private static readonly DateOnly BaseSanctionDate = new(2026, 1, 1);
    private static readonly DateOnly BaseMaturityDate = new(2026, 6, 30);

    [Fact]
    public void DemandLoan_Sanction_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        var acc = AccountNumber.Generate("0101", "451", 10001);
        var cif = new CifNumber("CIF-10001");
        var limit = Money.Bdt(1_000_000m);

        // Act
        var facility = new DemandLoanFacility(
            acc,
            cif,
            DemandLoanType.PaymentAgainstDocuments_PAD,
            "451",
            "PAD-LC-2026-001",
            "SEC-TRADE-01",
            "IND-TEXTILE-02",
            "0101",
            limit,
            12.0m,
            2.0m,
            BaseSanctionDate,
            BaseMaturityDate);

        // Assert
        facility.Id.Should().Be(acc);
        facility.Cif.Should().Be(cif);
        facility.Type.Should().Be(DemandLoanType.PaymentAgainstDocuments_PAD);
        facility.ProductCode.Should().Be("451");
        facility.LoanReferenceNumber.Should().Be("PAD-LC-2026-001");
        facility.OutstandingPrincipal.Amount.Should().Be(0m);
        facility.SanctionLimit.Amount.Should().Be(1_000_000m);
        facility.AnnualInterestRate.Should().Be(12.0m);
        facility.PenalInterestRate.Should().Be(2.0m);
        facility.Status.Should().Be(DemandLoanStatus.Sanctioned);
        facility.RepaymentPriority.Should().Be(RepaymentPriorityScheme.Standard);
    }

    [Fact]
    public void DemandLoan_Sanction_WithNonPositiveLimit_ThrowsInvariantViolation()
    {
        var acc = AccountNumber.Generate("0101", "451", 10002);
        var cif = new CifNumber("CIF-10002");

        var act = () => new DemandLoanFacility(
            acc,
            cif,
            DemandLoanType.LoanAgainstTrustReceipt_LTR,
            "452",
            "LTR-2026-001",
            "SEC-COMM-01",
            "IND-IMPORT-01",
            "0101",
            Money.Bdt(0m),
            11.5m,
            2.0m,
            BaseSanctionDate,
            BaseMaturityDate);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Demand loan sanction limit must be strictly positive*");
    }

    [Fact]
    public void DemandLoan_Sanction_WithMaturityBeforeSanction_ThrowsInvariantViolation()
    {
        var acc = AccountNumber.Generate("0101", "451", 10003);
        var cif = new CifNumber("CIF-10003");

        var act = () => new DemandLoanFacility(
            acc,
            cif,
            DemandLoanType.CleanDemandLoan,
            "454",
            "CDL-2026-001",
            "SEC-COMM-01",
            "IND-GENERAL",
            "0101",
            Money.Bdt(500_000m),
            13.0m,
            2.0m,
            BaseSanctionDate,
            new DateOnly(2025, 12, 31));

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Maturity date*must be strictly after sanction date*");
    }

    [Fact]
    public void DemandLoan_Disburse_IncreasesOutstandingPrincipal_AndSetsDisbursedStatus()
    {
        var acc = AccountNumber.Generate("0101", "451", 10004);
        var facility = CreateSampleFacility(acc, 1_000_000m);

        facility.Disburse(Money.Bdt(400_000m), new DateOnly(2026, 1, 5), "First Drawdown");

        facility.OutstandingPrincipal.Amount.Should().Be(400_000m);
        facility.Status.Should().Be(DemandLoanStatus.Disbursed);
        facility.DisbursedDate.Should().Be(new DateOnly(2026, 1, 5));
    }

    [Fact]
    public void DemandLoan_Disburse_ExceedingLimit_ThrowsInvariantViolation()
    {
        var acc = AccountNumber.Generate("0101", "451", 10005);
        var facility = CreateSampleFacility(acc, 500_000m);

        var act = () => facility.Disburse(Money.Bdt(600_000m), new DateOnly(2026, 1, 5));

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*would exceed sanctioned limit*");
    }

    [Fact]
    public void DemandLoan_AccrueDailyInterest_WhenPerforming_AccruesRegularInterest()
    {
        var acc = AccountNumber.Generate("0101", "451", 10006);
        var facility = CreateSampleFacility(acc, 1_000_000m, 12.0m, 2.0m);
        facility.Disburse(Money.Bdt(500_000m), BaseSanctionDate);

        // Act: Accrue on a performing date before maturity
        facility.AccrueDailyInterest(new DateOnly(2026, 1, 2));

        // Expected: (500000 * 0.12) / 365 = 164.38
        facility.RegularAccruedInterest.Amount.Should().Be(164.38m);
        facility.InterestSuspense.Amount.Should().Be(0m);
        facility.PenalInterest.Amount.Should().Be(0m);
        facility.Status.Should().Be(DemandLoanStatus.Disbursed);
    }

    [Fact]
    public void DemandLoan_AccrueDailyInterest_WhenOverdue_RoutesToInterestSuspense_AndAccruesPenal()
    {
        var acc = AccountNumber.Generate("0101", "451", 10007);
        var facility = CreateSampleFacility(acc, 1_000_000m, 12.0m, 2.0m);
        facility.Disburse(Money.Bdt(500_000m), BaseSanctionDate);

        // Act: Accrue on a date after maturity (Overdue)
        var overdueDate = BaseMaturityDate.AddDays(5);
        facility.AccrueDailyInterest(overdueDate);

        // Expected Regular: (500000 * 0.12) / 365 = 164.38 -> Routed to Interest Suspense (BRPD 4.5.2-ii)
        // Expected Penal: (500000 * 0.02) / 365 = 27.40
        facility.InterestSuspense.Amount.Should().Be(164.38m);
        facility.OverdueInterest.Amount.Should().Be(164.38m);
        facility.PenalInterest.Amount.Should().Be(27.40m);
        facility.RegularAccruedInterest.Amount.Should().Be(0m);
        facility.Status.Should().Be(DemandLoanStatus.Overdue);
    }

    [Fact]
    public void DemandLoan_CapitalizeInterest_CapitalizesAllAccruedInterestIntoPrincipal_AndClearsSuspense()
    {
        var acc = AccountNumber.Generate("0101", "451", 10008);
        var facility = CreateSampleFacility(acc, 2_000_000m, 12.0m, 2.0m);
        facility.Disburse(Money.Bdt(500_000m), BaseSanctionDate);

        // Accrue performing interest
        facility.AccrueDailyInterest(new DateOnly(2026, 1, 2)); // 164.38 Regular

        // Accrue overdue interest
        facility.AccrueDailyInterest(BaseMaturityDate.AddDays(10)); // 164.38 Overdue/Suspense + 27.40 Penal

        decimal totalAccrued = 164.38m + 164.38m + 27.40m; // 356.16m

        // Act: Capitalize interest schedule (BRPD 4.5.2-iii)
        facility.CapitalizeInterest("BB-BRPD-RESCHEDULE-2026-001", new DateOnly(2026, 7, 15));

        // Assert
        facility.OutstandingPrincipal.Amount.Should().Be(500_000m + totalAccrued);
        facility.RegularAccruedInterest.Amount.Should().Be(0m);
        facility.OverdueInterest.Amount.Should().Be(0m);
        facility.PenalInterest.Amount.Should().Be(0m);
        facility.InterestSuspense.Amount.Should().Be(0m);
        facility.Status.Should().Be(DemandLoanStatus.Restructured);
        facility.CapitalizedDate.Should().Be(new DateOnly(2026, 7, 15));
        facility.CapitalizationApprovalReference.Should().Be("BB-BRPD-RESCHEDULE-2026-001");
    }

    [Fact]
    public void DemandLoan_CapitalizeInterest_WithZeroInterest_ThrowsInvariantViolation()
    {
        var acc = AccountNumber.Generate("0101", "451", 10009);
        var facility = CreateSampleFacility(acc, 1_000_000m);
        facility.Disburse(Money.Bdt(500_000m), BaseSanctionDate);

        var act = () => facility.CapitalizeInterest("BB-TEST-REF", new DateOnly(2026, 1, 10));

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*No accrued or overdue interest available to capitalize*");
    }

    [Fact]
    public void DemandLoan_Repay_StandardPriority_DeductsCharges_Penal_Overdue_Regular_Principal()
    {
        var acc = AccountNumber.Generate("0101", "451", 10010);
        var facility = CreateSampleFacility(acc, 1_000_000m, 12.0m, 2.0m);
        facility.Disburse(Money.Bdt(100_000m), BaseSanctionDate);

        // Perform accruals to create overdue interest
        facility.AccrueDailyInterest(BaseMaturityDate.AddDays(1)); // 32.88 Overdue, 5.48 Penal

        // Repay partial amount: 20 BDT
        facility.Repay(Money.Bdt(20m), RepaymentSourceType.Transfer, new DateOnly(2026, 7, 5));

        // Penal was 5.48, so 5.48 penal is fully cleared; remaining 14.52 goes to OverdueInterest
        facility.PenalInterest.Amount.Should().Be(0m);
        facility.OverdueInterest.Amount.Should().Be(32.88m - 14.52m);
        facility.InterestSuspense.Amount.Should().Be(32.88m - 14.52m);
        facility.OutstandingPrincipal.Amount.Should().Be(100_000m);
    }

    [Fact]
    public void DemandLoan_Repay_FullLiquidation_SetsStatusToLiquidated()
    {
        var acc = AccountNumber.Generate("0101", "451", 10011);
        var facility = CreateSampleFacility(acc, 1_000_000m, 12.0m, 2.0m);
        facility.Disburse(Money.Bdt(50_000m), BaseSanctionDate);

        // Repay exactly 50,000 BDT
        facility.Repay(Money.Bdt(50_000m), RepaymentSourceType.Cash, new DateOnly(2026, 1, 10));

        facility.OutstandingPrincipal.Amount.Should().Be(0m);
        facility.Status.Should().Be(DemandLoanStatus.Liquidated);
    }

    [Fact]
    public void DemandLoan_ForcedPadFromImportBill_CreatesAndDisbursesImmediately()
    {
        var acc = AccountNumber.Generate("0101", "451", 10012);
        var cif = new CifNumber("CIF-TRADE-999");
        var billAmount = Money.Bdt(2_500_000m);

        var facility = DemandLoanFacility.CreateForcedPadFromImportBill(
            acc,
            cif,
            "0101",
            "LC-IMP-2026-888",
            "BL-DOC-777",
            billAmount,
            11.5m,
            2.0m,
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 3, 3));

        facility.ProductCode.Should().Be("451");
        facility.LoanReferenceNumber.Should().Be("PAD-LC-IMP-2026-888-BL-DOC-777");
        facility.Type.Should().Be(DemandLoanType.PaymentAgainstDocuments_PAD);
        facility.OutstandingPrincipal.Amount.Should().Be(2_500_000m);
        facility.Status.Should().Be(DemandLoanStatus.Disbursed);
    }

    private static DemandLoanFacility CreateSampleFacility(
        AccountNumber acc,
        decimal limitAmt,
        decimal annualRate = 12.0m,
        decimal penalRate = 2.0m,
        RepaymentPriorityScheme priority = RepaymentPriorityScheme.Standard)
    {
        return new DemandLoanFacility(
            acc,
            new CifNumber("CIF-10001"),
            DemandLoanType.PaymentAgainstDocuments_PAD,
            "451",
            "PAD-REF-001",
            "SEC-TRADE",
            "IND-TEXTILE",
            "0101",
            Money.Bdt(limitAmt),
            annualRate,
            penalRate,
            BaseSanctionDate,
            BaseMaturityDate,
            priority);
    }
}
