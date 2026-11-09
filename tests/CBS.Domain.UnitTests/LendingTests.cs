using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class LendingTests
{
    private static AccountNumber CreateValidAccountNumber() => AccountNumber.Generate("001", "201", 1);
    private static CifNumber CreateValidCif() => new("CIF123456789");

    [Fact]
    public void CollateralAsset_HaircutAndDrawingPower_CalculatesAccurately()
    {
        // Real Estate: MV 1,500,000 BDT, FSV 1,200,000 BDT, Haircut 25%
        // Eligible DP = 1,200,000 * 0.75 = 900,000 BDT
        var collateral = new CollateralAsset(
            "COL-RE-001",
            CollateralType.MortgageOnRealEstate,
            Money.Bdt(1500000m),
            Money.Bdt(1200000m),
            25m);

        collateral.CalculateEligibleDrawingPower().Should().Be(Money.Bdt(900000m));
    }

    [Fact]
    public void CollateralAsset_ForcedSaleValueExceedsMarketValue_ThrowsInvariantViolation()
    {
        var action = () => new CollateralAsset(
            "COL-RE-002",
            CollateralType.MortgageOnRealEstate,
            Money.Bdt(1000000m),
            Money.Bdt(1200000m), // FSV > MV
            20m);

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*ForcedSaleValueExceedsMarketValueInvariant*");
    }

    [Fact]
    public void LoanAccount_QuardHasana_WithNonZeroInterest_ThrowsQuardNonZeroInterestInvariant()
    {
        // Islamic Quard cannot carry interest or profit (BRPD 4.3.4-i)
        var action = () => new LoanAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            LoanFacilityType.Islamic_Quard,
            "001",
            Money.Bdt(50000m),
            Money.Bdt(50000m),
            interestRate: 2.5m, // Non-zero prohibited!
            new DateOnly(2026, 11, 1),
            new DateOnly(2027, 5, 1));

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*QuardNonZeroInterestInvariant*");
    }

    [Fact]
    public void LoanAccount_Disbursement_BeforeCadVetting_ThrowsCadChecklistNotVettedInvariant()
    {
        var loan = new LoanAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            LoanFacilityType.TermLoan,
            "001",
            Money.Bdt(500000m),
            Money.Bdt(400000m),
            9.00m,
            new DateOnly(2026, 11, 1),
            new DateOnly(2029, 11, 1));

        // Attempting to disburse without CAD vetting sign-off
        var action = () => loan.Disburse(Money.Bdt(200000m), "BRANCH_OFFICER_01");

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*CadChecklistNotVettedInvariant*");
    }

    [Fact]
    public void LoanAccount_Disbursement_WhenDisbursingOfficerEqualsCadOfficer_ThrowsMakerCheckerSeparation()
    {
        var loan = new LoanAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            LoanFacilityType.TermLoan,
            "001",
            Money.Bdt(500000m),
            Money.Bdt(400000m),
            9.00m,
            new DateOnly(2026, 11, 1),
            new DateOnly(2029, 11, 1));

        loan.VetCadChecklist("CAD_OFFICER_01");

        // Same officer attempts to disburse funds
        var action = () => loan.Disburse(Money.Bdt(200000m), "CAD_OFFICER_01");

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*MakerCheckerSeparationInvariant*");
    }

    [Fact]
    public void LoanAccount_Disbursement_ExceedingDrawingPower_ThrowsDrawingPowerExceededInvariant()
    {
        var loan = new LoanAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            LoanFacilityType.ContinuousLoan_CC,
            "001",
            Money.Bdt(1000000m), // Sanction limit 1M
            Money.Bdt(600000m),  // Drawing Power 600k based on stock inspection
            10.00m,
            new DateOnly(2026, 11, 1),
            new DateOnly(2027, 11, 1));

        loan.VetCadChecklist("CAD_OFFICER_01");

        // Requesting 700k exceeds 600k Drawing Power
        var action = () => loan.Disburse(Money.Bdt(700000m), "BRANCH_OFFICER_01");

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*DrawingPowerExceededInvariant*");
    }

    [Fact]
    public void LoanAccount_BaiMurabaha_WithoutSupplierBeneficiary_ThrowsMurabahaDirectVendorDisbursementInvariant()
    {
        var loan = new LoanAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            LoanFacilityType.Islamic_BaiMurabaha,
            "001",
            Money.Bdt(800000m),
            Money.Bdt(800000m),
            8.50m,
            new DateOnly(2026, 11, 1),
            new DateOnly(2027, 11, 1));

        loan.VetCadChecklist("CAD_OFFICER_01");

        // Direct cash / client credit without supplier Pay Order beneficiary is prohibited (BRPD 4.1.5-iv)
        var action = () => loan.Disburse(Money.Bdt(500000m), "BRANCH_OFFICER_01", supplierPayOrderBeneficiary: null);

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*MurabahaDirectVendorDisbursementInvariant*");
    }

    [Fact]
    public void LoanAccount_BaiMurabaha_WithSupplierBeneficiary_Succeeds()
    {
        var loan = new LoanAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            LoanFacilityType.Islamic_BaiMurabaha,
            "001",
            Money.Bdt(800000m),
            Money.Bdt(800000m),
            8.50m,
            new DateOnly(2026, 11, 1),
            new DateOnly(2027, 11, 1));

        loan.VetCadChecklist("CAD_OFFICER_01");

        loan.Disburse(Money.Bdt(500000m), "BRANCH_OFFICER_01", supplierPayOrderBeneficiary: "Rahim Steel Mills Ltd");

        loan.OutstandingPrincipal.Should().Be(Money.Bdt(500000m));
        loan.DisbursementBeneficiarySupplier.Should().Be("Rahim Steel Mills Ltd");
    }

    [Fact]
    public void LoanAccount_RegulatoryRecoveryWaterfall_StrictlyAllocatesInOrder()
    {
        var loan = new LoanAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            LoanFacilityType.TermLoan,
            "001",
            Money.Bdt(500000m),
            Money.Bdt(500000m),
            9.00m,
            new DateOnly(2026, 11, 1),
            new DateOnly(2029, 11, 1));

        loan.VetCadChecklist("CAD_OFFICER_01");
        loan.Disburse(Money.Bdt(100000m), "BRANCH_OFFICER_01");

        // Populate dues
        loan.SetCharges(Money.Bdt(2000m));
        loan.SetPenalInterest(Money.Bdt(3000m));
        loan.AccrueDailyInterest(days: 30); // ~739.73 BDT regular interest

        decimal initialCharges = loan.AccruedCharges.Amount; // 2,000
        decimal initialPenal = loan.PenalInterest.Amount;     // 3,000
        decimal initialRegular = loan.RegularAccruedInterest.Amount;

        // Payment of 4,000 BDT
        // Order: Charges (2,000) -> Penal (remaining 2,000) -> Overdue (0) -> Regular (0) -> Principal (0)
        var breakdown = loan.ProcessRepaymentWaterfall(Money.Bdt(4000m));

        breakdown.AllocatedCharges.Should().Be(Money.Bdt(2000m));
        breakdown.AllocatedPenalInterest.Should().Be(Money.Bdt(2000m));
        breakdown.AllocatedOverdueInterest.Should().Be(Money.Zero(Currency.BDT));
        breakdown.AllocatedRegularInterest.Should().Be(Money.Zero(Currency.BDT));
        breakdown.AllocatedPrincipal.Should().Be(Money.Zero(Currency.BDT));

        loan.AccruedCharges.Should().Be(Money.Zero(Currency.BDT));
        loan.PenalInterest.Should().Be(Money.Bdt(1000m)); // 3000 - 2000
        loan.RegularAccruedInterest.Amount.Should().Be(initialRegular);
        loan.OutstandingPrincipal.Should().Be(Money.Bdt(100000m));
    }

    [Fact]
    public void LoanAccount_AssetClassification_SuspendsInterestOnSubStandard()
    {
        var loan = new LoanAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            LoanFacilityType.ContinuousLoan_OD,
            "001",
            Money.Bdt(500000m),
            Money.Bdt(500000m),
            10.00m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2027, 1, 1));

        loan.VetCadChecklist("CAD_OFFICER_01");
        loan.Disburse(Money.Bdt(200000m), "BRANCH_OFFICER_01");

        // Regular interest accrual while unclassified
        loan.AccrueDailyInterest(days: 30);
        decimal initialRegular = loan.RegularAccruedInterest.Amount;
        initialRegular.Should().BeGreaterThan(0m);
        loan.InterestSuspense.Should().Be(Money.Zero(Currency.BDT));

        // Evaluate DPD 190 days -> SubStandard_SS (BRPD 4.1.11)
        loan.EvaluateClassification(daysPastDue: 190);

        loan.Classification.Should().Be(LoanClassificationGrade.SubStandard_SS);
        // Regular interest moved to Interest Suspense
        loan.RegularAccruedInterest.Should().Be(Money.Zero(Currency.BDT));
        loan.InterestSuspense.Amount.Should().Be(initialRegular);

        // Subsequent interest accrual goes directly to Interest Suspense (CEASE P&L INCOME)
        loan.AccrueDailyInterest(days: 30);
        loan.RegularAccruedInterest.Should().Be(Money.Zero(Currency.BDT));
        loan.InterestSuspense.Amount.Should().BeGreaterThan(initialRegular);
    }
}
