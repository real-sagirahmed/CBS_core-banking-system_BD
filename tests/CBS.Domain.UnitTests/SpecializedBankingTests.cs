using CBS.Domain.BranchCash;
using CBS.Domain.Deposits.Multiplication;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Lending.Agricultural;
using CBS.Domain.Lending.Discounted;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class SpecializedBankingTests
{
    // =========================================================================
    // 1. AGRICULTURAL CREDIT & KRISHI CARD TESTS (BRPD MODULE 4.4)
    // =========================================================================

    [Fact]
    public void AgriLoan_ShouldCreateAndDisburse_WhenKrishiCardAnd10TakaAccountPresent()
    {
        // ARCHITECTURAL INTENT: Agricultural Credit Mandate (BRPD 4.4.2)
        // Must have valid Krishi Card number and linked 10-Taka farmer savings account.
        var acc = AccountNumber.Generate("0101", "701", 10001);
        var farmerSavings = AccountNumber.Generate("0101", "101", 10002);
        var cif = new CifNumber("CIF-AGRI-001");

        var facility = new AgriculturalLoanFacility(
            acc,
            cif,
            "KC-DINAJPUR-99881",
            farmerSavings,
            CropSeasonType.Aman,
            "Khatiyan-1044/Daag-89",
            "UP-REF-2026-99",
            "0101",
            Money.Bdt(50000m),
            8.0m, // 8% simple interest
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 12, 1));

        facility.Status.Should().Be(AgriculturalLoanStatus.Initiated);
        facility.KrishiCardNumber.Should().Be("KC-DINAJPUR-99881");
        facility.FarmerSavingsAccount.Value.Should().Be(farmerSavings.Value);

        // Act: Disburse
        facility.Disburse(new DateOnly(2026, 6, 5));

        // Assert
        facility.Status.Should().Be(AgriculturalLoanStatus.Disbursed);
        facility.OutstandingPrincipal.Amount.Should().Be(50000m);
        facility.DisbursedDate.Should().Be(new DateOnly(2026, 6, 5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AgriLoan_ShouldThrowException_WhenKrishiCardIsMissing(string invalidCard)
    {
        var acc = AccountNumber.Generate("0101", "701", 10002);
        var farmerSavings = AccountNumber.Generate("0101", "101", 10002);
        var cif = new CifNumber("CIF-AGRI-002");

        var act = () => new AgriculturalLoanFacility(
            acc,
            cif,
            invalidCard,
            farmerSavings,
            CropSeasonType.Boro,
            "Porcha-12",
            "UP-01",
            "0101",
            Money.Bdt(40000m),
            8.0m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Krishi Card*");
    }

    [Fact]
    public void AgriLoan_ShouldAccrueSimpleInterest_WithoutIntraYearCompounding()
    {
        // ARCHITECTURAL INTENT: BRPD 4.4.15 Simple Interest Invariant
        // Agri credit strictly uses simple interest: (Principal * Rate * Days) / 365. No intra-year compounding.
        var acc = AccountNumber.Generate("0101", "701", 10003);
        var farmerSavings = AccountNumber.Generate("0101", "101", 10003);
        var cif = new CifNumber("CIF-AGRI-003");

        var facility = new AgriculturalLoanFacility(
            acc,
            cif,
            "KC-RANGPUR-11223",
            farmerSavings,
            CropSeasonType.Horticulture,
            "Porcha-77",
            "UP-99",
            "0101",
            Money.Bdt(100000m),
            8.0m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        facility.Disburse(new DateOnly(2026, 1, 1));

        // Act: Accrue for 180 days -> 100,000 * 0.08 * 180 / 365 = 3,945.21
        facility.AccrueSimpleInterest(new DateOnly(2026, 1, 1).AddDays(180));

        // Assert
        facility.AccruedSimpleInterest.Amount.Should().Be(3945.21m);

        // Accrue another 90 days (total 270 days) -> 100,000 * 0.08 * 270 / 365 = 5,917.81
        facility.AccrueSimpleInterest(new DateOnly(2026, 1, 1).AddDays(270));
        facility.AccruedSimpleInterest.Amount.Should().Be(5917.81m);
    }

    [Fact]
    public void AgriLoan_ShouldApplyRepaymentWaterfall_ChargesFirstThenInterestThenPrincipal()
    {
        // ARCHITECTURAL INTENT: Statutory Repayment Waterfall (BRPD 4.4.8-ii)
        // 1. Charges -> 2. Simple Interest -> 3. Principal
        var acc = AccountNumber.Generate("0101", "701", 10004);
        var farmerSavings = AccountNumber.Generate("0101", "101", 10004);
        var cif = new CifNumber("CIF-AGRI-004");

        var facility = new AgriculturalLoanFacility(
            acc,
            cif,
            "KC-BOGURA-44556",
            farmerSavings,
            CropSeasonType.PotatoWheat,
            "Porcha-88",
            "UP-44",
            "0101",
            Money.Bdt(50000m),
            8.0m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        facility.Disburse(new DateOnly(2026, 1, 1));
        facility.AccrueSimpleInterest(new DateOnly(2026, 1, 1).AddDays(100)); // 50,000 * 0.08 * 100 / 365 = 1,095.89 interest
        facility.AddCharge(Money.Bdt(500m));

        facility.AccruedCharges.Amount.Should().Be(500m);
        facility.AccruedSimpleInterest.Amount.Should().Be(1095.89m);
        facility.OutstandingPrincipal.Amount.Should().Be(50000m);

        // Act: Farmer repays 1,200 BDT
        // Waterfall: 500 pays all charges, remaining 700 reduces interest (1,095.89 - 700 = 395.89). Principal untouched.
        facility.Repay(Money.Bdt(1200m));

        facility.AccruedCharges.Amount.Should().Be(0m);
        facility.AccruedSimpleInterest.Amount.Should().Be(395.89m);
        facility.OutstandingPrincipal.Amount.Should().Be(50000m);

        // Act 2: Farmer repays 1,000 BDT
        // Waterfall: 395.89 clears interest, remaining 604.11 reduces principal
        facility.Repay(Money.Bdt(1000m));

        facility.AccruedCharges.Amount.Should().Be(0m);
        facility.AccruedSimpleInterest.Amount.Should().Be(0m);
        facility.OutstandingPrincipal.Amount.Should().Be(50000m - 604.11m);
    }

    [Fact]
    public void AgriLoan_ShouldRescheduleForDisaster_WithNonAccrualStatus()
    {
        // ARCHITECTURAL INTENT: Disaster Relief Rescheduling (BRPD 4.4.19-ii)
        // Must freeze further interest recognition into P&L (IsNonAccrual = true).
        var acc = AccountNumber.Generate("0101", "701", 10005);
        var farmerSavings = AccountNumber.Generate("0101", "101", 10005);
        var cif = new CifNumber("CIF-AGRI-005");

        var facility = new AgriculturalLoanFacility(
            acc,
            cif,
            "KC-SYLHET-77889",
            farmerSavings,
            CropSeasonType.Aman,
            "Porcha-90",
            "UP-Flood-Zone",
            "0101",
            Money.Bdt(80000m),
            8.0m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 8, 30));

        facility.Disburse(new DateOnly(2026, 1, 5));
        facility.AccrueSimpleInterest(new DateOnly(2026, 3, 5));

        // Act: Reschedule due to severe flood
        facility.RescheduleForDisaster(
            new DateOnly(2027, 8, 30),
            "MoA-Disaster-Gazette-Flood-2026",
            new DateOnly(2026, 8, 15));

        // Assert
        facility.Status.Should().Be(AgriculturalLoanStatus.RescheduledNonAccrual);
        facility.IsNonAccrual.Should().BeTrue();
        facility.HarvestMaturityDate.Should().Be(new DateOnly(2027, 8, 30));

        // Invariant: Non-accrual facility ignores further P&L interest accrual
        var interestBefore = facility.AccruedSimpleInterest.Amount;
        facility.AccrueSimpleInterest(new DateOnly(2026, 9, 30));
        facility.AccruedSimpleInterest.Amount.Should().Be(interestBefore);
    }

    // =========================================================================
    // 2. BILL DISCOUNTING & INLAND BILL PURCHASE (BRPD MODULE 4.6)
    // =========================================================================

    [Fact]
    public void BillDiscounting_ShouldCalculateUpfrontDiscount_AndDisburseNetProceeds()
    {
        // ARCHITECTURAL INTENT: Commercial Bill Purchase (BRPD 4.6.2)
        // Face Value: 1,000,000 BDT, Tenor: 90 days, Discount Rate: 9.0%
        // Upfront Discount = (1,000,000 * 9.0 * 90) / 36,500 = 22,191.78 BDT
        // Net Disbursed = 1,000,000 - 22,191.78 = 977,808.22 BDT
        var acc = AccountNumber.Generate("0101", "601", 10001);
        var cif = new CifNumber("CIF-CORP-901");

        var facility = new BillDiscountingFacility(
            acc,
            cif,
            "IBP-2026-EXP-001",
            "125270115",
            "Sonali Bank PLC, Principal Branch",
            "0101",
            Money.Bdt(1000000m),
            9.0m,
            90,
            new DateOnly(2026, 5, 1));

        facility.Status.Should().Be(BillDiscountingStatus.Discounted);
        facility.UpfrontDiscountAmount.Amount.Should().Be(22191.78m);
        facility.NetDisbursedAmount.Amount.Should().Be(977808.22m);
        facility.UnearnedDiscountBalance.Amount.Should().Be(22191.78m);
        facility.RecognizedPnLInterest.Amount.Should().Be(0m);
    }

    [Fact]
    public void BillDiscounting_ShouldAmortizeDailyUnearnedInterest_OnStraightLineBasis()
    {
        // ARCHITECTURAL INTENT: Straight-line unearned interest amortization (BRPD 4.6.2-i)
        // Daily Amortization = 22,191.78 / 90 = 246.5753 BDT/day
        var acc = AccountNumber.Generate("0101", "601", 10002);
        var cif = new CifNumber("CIF-CORP-902");

        var facility = new BillDiscountingFacility(
            acc,
            cif,
            "IBP-2026-EXP-002",
            "125270115",
            "Agrani Bank PLC",
            "0101",
            Money.Bdt(1000000m),
            9.0m,
            90,
            new DateOnly(2026, 5, 1));

        // Act: Amortize 30 days
        facility.AmortizeDailyUnearnedInterest(30);

        // Assert: 30 * (22,191.78 / 90) = 7,397.26
        facility.RecognizedPnLInterest.Amount.Should().Be(7397.26m);
        facility.UnearnedDiscountBalance.Amount.Should().Be(22191.78m - 7397.26m);
    }

    [Fact]
    public void BillDiscounting_ShouldEarlyRetireWithRebate_ReversingRemainingUnearnedDiscount()
    {
        // ARCHITECTURAL INTENT: Early retirement rebate (BRPD 4.6.2-ii)
        var acc = AccountNumber.Generate("0101", "601", 10003);
        var cif = new CifNumber("CIF-CORP-903");

        var facility = new BillDiscountingFacility(
            acc,
            cif,
            "IBP-2026-EXP-003",
            "125270115",
            "Janata Bank PLC",
            "0101",
            Money.Bdt(1000000m),
            9.0m,
            90,
            new DateOnly(2026, 5, 1));

        facility.AmortizeDailyUnearnedInterest(30); // 7,397.26 amortized, 14,794.52 unearned
        decimal remainingUnearned = facility.UnearnedDiscountBalance.Amount;

        // Act: Early settlement
        var rebate = facility.EarlyRetireAndRebate();

        // Assert
        rebate.Amount.Should().Be(remainingUnearned);
        facility.UnearnedDiscountBalance.Amount.Should().Be(0m);
        facility.Status.Should().Be(BillDiscountingStatus.EarlyRetiredRebated);
    }

    [Fact]
    public void BillDiscounting_ShouldLiquidateFacility_UponDraweeRealization()
    {
        // ARCHITECTURAL INTENT: Liquidation upon realization from drawee bank (BRPD 4.6.4)
        var acc = AccountNumber.Generate("0101", "601", 10004);
        var cif = new CifNumber("CIF-CORP-904");

        var facility = new BillDiscountingFacility(
            acc,
            cif,
            "IBP-2026-EXP-004",
            "125270115",
            "Rupali Bank PLC",
            "0101",
            Money.Bdt(500000m),
            10.0m,
            60,
            new DateOnly(2026, 5, 1));

        // Act: Realization from Drawee Bank
        facility.LiquidateFromDraweeRealization(Money.Bdt(500000m));

        // Assert
        facility.Status.Should().Be(BillDiscountingStatus.RealizedDraweePaid);
        facility.UnearnedDiscountBalance.Amount.Should().Be(0m);
    }

    // =========================================================================
    // 3. DEPOSIT MULTIPLICATION SCHEME TESTS (BRPD MODULE 3.7)
    // =========================================================================

    [Fact]
    public void DepositMultiplication_ShouldCalculateTenor_UsingLogarithmicFormula()
    {
        // ARCHITECTURAL INTENT: Tenor Formula Lock (BRPD 3.7.2)
        // Double Scheme (Multiplier = 2) at 8% annual rate:
        // n = ln(2) / ln(1 + 0.08/12) = 0.693147 / 0.0066445 = ~104.31 -> 105 months (ceiling)
        var acc = AccountNumber.Generate("0101", "301", 10001);
        var cif = new CifNumber("CIF-DEP-001");

        var account = new DepositMultiplicationAccount(
            acc,
            cif,
            "0101",
            Money.Bdt(100000m),
            2, // Double Scheme
            8.0m, // 8.0% annual rate
            new DateOnly(2026, 1, 1));

        account.MultiplierFactor.Should().Be(2);
        account.MaturityAmount.Amount.Should().Be(200000m);
        account.CalculatedTenorMonths.Should().Be(105);
        account.MaturityDate.Should().Be(new DateOnly(2026, 1, 1).AddMonths(105));
    }

    [Fact]
    public void DepositMultiplication_ShouldThrowException_WhenAttemptingInterBranchTransfer()
    {
        // ARCHITECTURAL INTENT: Statutory Prohibition of Inter-Branch Scheme Transfers (BRPD 3.7.5)
        var acc = AccountNumber.Generate("0101", "301", 10002);
        var cif = new CifNumber("CIF-DEP-002");

        var account = new DepositMultiplicationAccount(
            acc,
            cif,
            "0101",
            Money.Bdt(100000m),
            2,
            8.0m,
            new DateOnly(2026, 1, 1));

        var act = () => account.TransferToOtherBranch("0202");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Transfer of fixed deposit multiplication schemes between branches is prohibited*");
    }

    // =========================================================================
    // 4. INTER-BRANCH REMITTANCE & CRYPTOGRAPHIC TEST KEY TESTS (BRPD 3.12, 3.13)
    // =========================================================================

    [Fact]
    public void CryptographicTestKey_ShouldGenerateDeterministicKey_AndVerifyZeroTolerance()
    {
        // Test key verification with identical inputs must generate identical 5-digit key
        var date = new DateOnly(2026, 5, 20);
        decimal amount = 75000m;
        string issuingBranch = "0101";
        string payeeBranch = "0202";

        var key1 = CryptographicTestKeyService.GenerateTestKey(date, amount, issuingBranch, payeeBranch);
        var key2 = CryptographicTestKeyService.GenerateTestKey(date, amount, issuingBranch, payeeBranch);

        key1.Should().Be(key2);
        key1.Length.Should().Be(5);

        // Verification
        CryptographicTestKeyService.VerifyTestKey(key1, date, amount, issuingBranch, payeeBranch).Should().BeTrue();
        CryptographicTestKeyService.VerifyTestKey("99999", date, amount, issuingBranch, payeeBranch).Should().BeFalse();
    }

    [Fact]
    public void InterBranchInstrument_ShouldThrowException_WhenSanctionScreeningFails()
    {
        // ARCHITECTURAL INTENT: Mandatory UNSCR Sanction Clearance (BRPD 3.12.3)
        var act = () => new InterBranchPaymentInstrument(
            "DD-2026-00001",
            InterBranchInstrumentType.DemandDraft,
            Money.Bdt(100000m),
            Money.Bdt(150m),
            Money.Bdt(22.50m),
            "0101",
            "0202",
            "Prohibited Entity XYZ",
            "Remitter John",
            new DateOnly(2026, 5, 20),
            isSanctionScreened: false); // Failed / missing clearance

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*UNSCR & local sanction*");
    }

    [Fact]
    public void InterBranchInstrument_ShouldVerifyAndPay_WithValidTestKey()
    {
        // Issue instrument with sanction cleared
        var instrument = new InterBranchPaymentInstrument(
            "TT-2026-00002",
            InterBranchInstrumentType.TelegraphicTransfer,
            Money.Bdt(250000m),
            Money.Bdt(200m),
            Money.Bdt(30m),
            "0101",
            "0303",
            "ABC Traders Ltd",
            "XYZ Corporation",
            new DateOnly(2026, 5, 20),
            isSanctionScreened: true,
            beneficiaryAccountNumber: "0303-101-99887");

        instrument.Status.Should().Be(InterBranchInstrumentStatus.IssuedPendingAdvice);
        instrument.ContraEntryPosted.Should().BeTrue();

        string validTestKey = instrument.CryptographicTestKey;

        // Act: Verify and pay at Payee branch
        instrument.VerifyAndPay(validTestKey, "TELLER_0303", new DateOnly(2026, 5, 20));

        // Assert
        instrument.Status.Should().Be(InterBranchInstrumentStatus.PaidSettled);
        instrument.PaidByUserId.Should().Be("TELLER_0303");
        instrument.PaidDate.Should().Be(new DateOnly(2026, 5, 20));
    }

    [Fact]
    public void InterBranchInstrument_ShouldRejectPayment_WhenTestKeyIsIncorrect()
    {
        var instrument = new InterBranchPaymentInstrument(
            "TT-2026-00003",
            InterBranchInstrumentType.TelegraphicTransfer,
            Money.Bdt(250000m),
            Money.Bdt(200m),
            Money.Bdt(30m),
            "0101",
            "0303",
            "ABC Traders Ltd",
            "XYZ Corporation",
            new DateOnly(2026, 5, 20),
            isSanctionScreened: true);

        // Act: Wrong test key
        var act = () => instrument.VerifyAndPay("00000", "TELLER_0303", new DateOnly(2026, 5, 20));

        // Assert
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Cryptographic Test Key verification failed*");
    }
}
