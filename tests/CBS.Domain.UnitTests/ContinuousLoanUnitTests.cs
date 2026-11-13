// ARCHITECTURAL INTENT: Domain Unit Tests for Continuous Loan (Cash Credit / Overdraft - CC/OD) Facilities
// Locking dynamic Drawing Power (DP), Stock & Book Debts Statements, Godown Inspections, Insurance Adequacy,
// Temporary Overdraft (TOD), Annual Renewal Governance, and Quarterly Interest Capitalization
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 and 3.9.

using System;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Lending.Continuous;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class ContinuousLoanUnitTests
{
    private readonly DateOnly _today = new(2026, 11, 13);
    private readonly AccountNumber _testAccountNumber = AccountNumber.From("0101201000018");
    private readonly CifNumber _testCif = new("CIF123456");

    // =========================================================================
    // 1. ORIGINATION & INVARIANT VALIDATION TESTS (BRPD 4.1.3, 4.1.4, 4.1.6)
    // =========================================================================

    [Fact]
    public void ContinuousLoanFacility_ValidOrigination_SetsAllInitialStateCorrectly()
    {
        var expiry = _today.AddYears(1);
        var facility = new ContinuousLoanFacility(
            accountNumber: _testAccountNumber,
            cif: _testCif,
            branchCode: "BR01",
            category: ContinuousLoanCategory.CashCredit_Hypothecation,
            sanctionLimit: Money.Bdt(5000000m),
            initialDrawingPower: Money.Bdt(4000000m),
            regularInterestRate: 9.50m,
            penalInterestRate: 11.50m,
            sanctionDate: _today,
            expiryDate: expiry,
            stockMarginPercent: 25.00m,
            bookDebtsMarginPercent: 35.00m);

        facility.Id.Should().Be(_testAccountNumber);
        facility.Cif.Should().Be(_testCif);
        facility.BranchCode.Should().Be("BR01");
        facility.Category.Should().Be(ContinuousLoanCategory.CashCredit_Hypothecation);
        facility.SanctionLimit.Amount.Should().Be(5000000m);
        facility.DrawingPower.Amount.Should().Be(4000000m);
        facility.OutstandingPrincipal.Amount.Should().Be(0m);
        facility.RegularInterestRate.Should().Be(9.50m);
        facility.PenalInterestRate.Should().Be(11.50m);
        facility.SanctionDate.Should().Be(_today);
        facility.ExpiryDate.Should().Be(expiry);
        facility.RenewalStatus.Should().Be(ContinuousLoanRenewalStatus.Active);
        facility.RenewalCount.Should().Be(0);
        facility.Classification.Should().Be(LoanClassificationGrade.Standard);
        facility.IsDebitRestricted.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50000)]
    public void ContinuousLoanFacility_ZeroOrNegativeSanctionLimit_ThrowsInvalidSanctionLimitInvariant(decimal invalidLimit)
    {
        var act = () => new ContinuousLoanFacility(
            accountNumber: _testAccountNumber,
            cif: _testCif,
            branchCode: "BR01",
            category: ContinuousLoanCategory.CashCredit_Hypothecation,
            sanctionLimit: Money.Bdt(invalidLimit),
            initialDrawingPower: Money.Zero(Currency.BDT),
            regularInterestRate: 9.50m,
            penalInterestRate: 11.50m,
            sanctionDate: _today,
            expiryDate: _today.AddYears(1));

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("InvalidSanctionLimitInvariant");
    }

    [Fact]
    public void ContinuousLoanFacility_InitialDrawingPowerExceedsSanctionLimit_ThrowsDrawingPowerExceedsSanctionLimitInvariant()
    {
        var act = () => new ContinuousLoanFacility(
            accountNumber: _testAccountNumber,
            cif: _testCif,
            branchCode: "BR01",
            category: ContinuousLoanCategory.SecuredOverdraft_General,
            sanctionLimit: Money.Bdt(1000000m),
            initialDrawingPower: Money.Bdt(1200000m), // Exceeds Sanction Limit
            regularInterestRate: 9.50m,
            penalInterestRate: 11.50m,
            sanctionDate: _today,
            expiryDate: _today.AddYears(1));

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("DrawingPowerExceedsSanctionLimitInvariant");
    }

    // =========================================================================
    // 2. STOCK & BOOK DEBTS STATEMENT & DP CALCULATION TESTS (BRPD 4.1.4, 4.1.6)
    // =========================================================================

    [Fact]
    public void StockStatement_ComputesCorrectDrawingPower_ExcludingOver90DaysDebts()
    {
        // Stock: Raw 1M + WIP 0.5M + Finished 1.5M = 3M gross. Creditors: 0.5M -> Paid stock = 2.5M.
        // Stock Margin: 25% -> Eligible Stock DP = 2.5M * 0.75 = 1,875,000.
        // Book Debts: <=90 days: 800,000, >90 days: 300,000 (ineligible).
        // Debts Margin: 35% -> Eligible Debts DP = 800,000 * 0.65 = 520,000.
        // Total DP = 1,875,000 + 520,000 = 2,395,000.
        var stmt = new ContinuousLoanStockStatement(
            statementId: "STMT-001",
            loanAccountNumber: _testAccountNumber.Value,
            statementDate: _today,
            asOfPeriodDate: _today.AddDays(-5),
            rawMaterialsValue: Money.Bdt(1000000m),
            workInProgressValue: Money.Bdt(500000m),
            finishedGoodsValue: Money.Bdt(1500000m),
            creditorsForGoods: Money.Bdt(500000m),
            bookDebtsUnder90Days: Money.Bdt(800000m),
            bookDebtsOver90Days: Money.Bdt(300000m), // Ineligible
            stockMarginPercent: 25.00m,
            bookDebtsMarginPercent: 35.00m,
            submittedByBorrowerRepresentative: "Managing Director",
            vettedByOfficerUserId: "CAD_OFFICER_01");

        stmt.TotalStockValue.Amount.Should().Be(3000000m);
        stmt.PaidStockValue.Amount.Should().Be(2500000m);
        stmt.EligibleStockDrawingPower.Amount.Should().Be(1875000m);
        stmt.EligibleBookDebtsDrawingPower.Amount.Should().Be(520000m);
        stmt.TotalCalculatedDrawingPower.Amount.Should().Be(2395000m);
    }

    [Fact]
    public void SubmitStockStatement_UpdatesFacilityDrawingPower_CappedAtSanctionLimit()
    {
        var facility = CreateSampleFacility(sanctionLimit: 2000000m, initialDp: 1000000m);

        var stmt = new ContinuousLoanStockStatement(
            statementId: "STMT-001",
            loanAccountNumber: _testAccountNumber.Value,
            statementDate: _today,
            asOfPeriodDate: _today.AddDays(-2),
            rawMaterialsValue: Money.Bdt(2000000m),
            workInProgressValue: Money.Bdt(1000000m),
            finishedGoodsValue: Money.Bdt(2000000m),
            creditorsForGoods: Money.Bdt(500000m),
            bookDebtsUnder90Days: Money.Bdt(1000000m),
            bookDebtsOver90Days: Money.Bdt(0m),
            stockMarginPercent: 25.00m,
            bookDebtsMarginPercent: 35.00m,
            submittedByBorrowerRepresentative: "CFO",
            vettedByOfficerUserId: "OFFICER_01");

        // Calculated DP is 4.5M * 0.75 + 1M * 0.65 = 3,375,000 + 650,000 = 4,025,000.
        // But Sanction Limit is 2,000,000 -> Drawing Power must be capped at 2,000,000.
        facility.SubmitStockStatement(stmt);

        facility.DrawingPower.Amount.Should().Be(2000000m);
        facility.LastStockStatementDate.Should().Be(_today);
    }

    // =========================================================================
    // 3. GODOWN INSPECTION & INSURANCE ADEQUACY TESTS (BRPD 4.1.3-iv)
    // =========================================================================

    [Fact]
    public void GodownInspection_AdequateInsurance_MaintainsInsurableStock()
    {
        var insp = new ContinuousLoanGodownInspection(
            inspectionId: "INSP-001",
            loanAccountNumber: _testAccountNumber.Value,
            inspectionDate: _today,
            inspectorUserId: "CAD_INSPECTOR_01",
            godownLocation: "Tejgaon Industrial Area, Godown #4",
            inspectedStockValue: Money.Bdt(3000000m),
            stockConditionDescription: "Sound condition, properly stacked in bins",
            insurancePolicyNumber: "INS-POL-2026-9999",
            insuranceCompany: "Green Delta Insurance PLC",
            insuranceExpiryDate: _today.AddMonths(6),
            insuranceSumInsured: Money.Bdt(4000000m), // Sum insured > Stock
            inspectionResult: StockInspectionResult.Satisfactory,
            remarks: "Routine quarterly inspection");

        insp.IsInsuranceValid.Should().BeTrue();
        insp.IsInsuranceAdequate.Should().BeTrue();
        insp.InsurableCappedStockValue.Amount.Should().Be(3000000m);
    }

    [Fact]
    public void GodownInspection_ExpiredInsurance_ZeroesInsurableStockAndDeflatesDp()
    {
        var facility = CreateSampleFacility(sanctionLimit: 5000000m, initialDp: 3000000m);

        var insp = new ContinuousLoanGodownInspection(
            inspectionId: "INSP-002",
            loanAccountNumber: _testAccountNumber.Value,
            inspectionDate: _today,
            inspectorUserId: "CAD_INSPECTOR_01",
            godownLocation: "Chittagong Port Yard #2",
            inspectedStockValue: Money.Bdt(3000000m),
            stockConditionDescription: "Sound condition",
            insurancePolicyNumber: "INS-EXPIRED-1234",
            insuranceCompany: "Reliance Insurance",
            insuranceExpiryDate: _today.AddDays(-5), // EXPIRED INSURANCE POLICY
            insuranceSumInsured: Money.Bdt(5000000m),
            inspectionResult: StockInspectionResult.InadequateInsurance,
            remarks: "Policy expired 5 days ago");

        insp.IsInsuranceValid.Should().BeFalse();
        insp.InsurableCappedStockValue.Amount.Should().Be(0m);

        facility.RecordGodownInspection(insp);
        facility.DrawingPower.Amount.Should().Be(0m); // Deflated due to expired insurance
        facility.LastInspectionDate.Should().Be(_today);
    }

    // =========================================================================
    // 4. DRAWING POWER & WITHDRAWAL GATE TESTS (BRPD 4.1.6)
    // =========================================================================

    [Fact]
    public void Withdraw_WithinDrawingPower_SucceedsAndIncreasesOutstanding()
    {
        var facility = CreateSampleFacility(sanctionLimit: 5000000m, initialDp: 3000000m);

        facility.Withdraw(
            amount: Money.Bdt(1500000m),
            channelOrVoucher: "CHEQUE-001234",
            approvedByUserId: "TELLER_01",
            asOfDate: _today);

        facility.OutstandingPrincipal.Amount.Should().Be(1500000m);
    }

    [Fact]
    public void Withdraw_ExceedingDrawingPower_ThrowsDrawingPowerExceededInvariant()
    {
        var facility = CreateSampleFacility(sanctionLimit: 5000000m, initialDp: 2000000m);

        // Attempting to withdraw 2,500,000 when DP is only 2,000,000
        var act = () => facility.Withdraw(
            amount: Money.Bdt(2500000m),
            channelOrVoucher: "CHEQUE-001235",
            approvedByUserId: "TELLER_01",
            asOfDate: _today);

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("DrawingPowerExceededInvariant");
    }

    // =========================================================================
    // 5. TEMPORARY OVERDRAFT (TOD) / EOL TESTS (BRPD 3.9.1-iii, 3.9.1-iv)
    // =========================================================================

    [Fact]
    public void TemporaryOverdraft_EnhancesAvailableDrawing_AndAllowsWithdrawal()
    {
        var facility = CreateSampleFacility(sanctionLimit: 2000000m, initialDp: 2000000m);

        // Grant TOD of 500,000 for 15 days
        facility.GrantTemporaryOverdraft(
            amount: Money.Bdt(500000m),
            expiryDate: _today.AddDays(15),
            penalRate: 13.50m,
            approvedByUserId: "HEAD_OF_CREDIT",
            approvalReference: "HOC-TOD-2026-0042");

        facility.CurrentTodAmount.Should().NotBeNull();
        facility.CurrentTodAmount!.Amount.Should().Be(500000m);

        // Can now withdraw 2,300,000 (exceeding regular limit of 2,000,000)
        facility.Withdraw(
            amount: Money.Bdt(2300000m),
            channelOrVoucher: "RTGS-TOD-001",
            approvedByUserId: "BRANCH_MGR",
            asOfDate: _today);

        facility.OutstandingPrincipal.Amount.Should().Be(2300000m);
    }

    [Fact]
    public void TemporaryOverdraft_AfterExpiry_RevertsLimit()
    {
        var facility = CreateSampleFacility(sanctionLimit: 2000000m, initialDp: 2000000m);
        facility.GrantTemporaryOverdraft(
            amount: Money.Bdt(500000m),
            expiryDate: _today.AddDays(10),
            penalRate: 13.50m,
            approvedByUserId: "HOC",
            approvalReference: "TOD-REF-1");

        // Fast forward to day 11 (TOD expired)
        facility.CheckAndExpireTod(_today.AddDays(11));

        facility.CurrentTodAmount.Should().BeNull();
        facility.CurrentTodExpiryDate.Should().BeNull();
    }

    // =========================================================================
    // 6. EXPIRED / UNRENEWED CONTINUOUS LOAN GOVERNANCE (BRPD 3.9.5, 4.1.9, 4.1.13)
    // =========================================================================

    [Fact]
    public void ExpiredFacility_DebitBlockedWithoutOverride_DepositAllowed()
    {
        var facility = CreateSampleFacility(sanctionLimit: 2000000m, initialDp: 2000000m);
        facility.Withdraw(Money.Bdt(1000000m), "CHQ-1", "OFFICER", _today);

        // Fast forward beyond Expiry Date without renewal
        var expiredDate = _today.AddYears(1).AddDays(1);
        facility.CheckAndMarkExpired(expiredDate);

        facility.RenewalStatus.Should().Be(ContinuousLoanRenewalStatus.Expired);
        facility.IsDebitRestricted.Should().BeTrue();

        // 1. Debit withdrawal blocked without override
        var act = () => facility.Withdraw(Money.Bdt(100000m), "CHQ-2", "OFFICER", expiredDate, isOverrideApproved: false);
        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("ExpiredContinuousLoanDebitBlockedInvariant");

        // 2. Debit withdrawal WITH override succeeds (BRPD 3.9.5-ii)
        facility.Withdraw(Money.Bdt(100000m), "CHQ-OVERRIDE", "MD_APPROVED", expiredDate, isOverrideApproved: true);
        facility.OutstandingPrincipal.Amount.Should().Be(1100000m);

        // 3. Credit / Deposit always unrestricted (BRPD 3.9.5-i)
        facility.Deposit(Money.Bdt(200000m));
        facility.OutstandingPrincipal.Amount.Should().Be(900000m);
    }

    [Fact]
    public void ExpiredFacility_AccruesPenalInterestOnDailyBalance()
    {
        var facility = CreateSampleFacility(sanctionLimit: 2000000m, initialDp: 2000000m);
        facility.Withdraw(Money.Bdt(1000000m), "CHQ-1", "OFFICER", _today);

        var expiredDate = _today.AddYears(1).AddDays(1);
        facility.CheckAndMarkExpired(expiredDate);

        // Daily accrual on expired facility should apply PenalInterestRate (11.50% vs regular 9.50%)
        facility.AccrueDailyInterest(expiredDate, days: 30);

        facility.RegularAccruedInterest.Amount.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void RenewFacility_ExtendsExpiryByOneYear_AndResetsStatusToRenewed()
    {
        var facility = CreateSampleFacility(sanctionLimit: 2000000m, initialDp: 2000000m);
        var expiredDate = _today.AddYears(1).AddDays(1);
        facility.CheckAndMarkExpired(expiredDate);

        var newExpiry = expiredDate.AddYears(1);
        facility.RenewFacility(
            newLimit: Money.Bdt(2500000m),
            newExpiryDate: newExpiry,
            newInterestRate: 9.00m,
            approvedByUserId: "BOARD_CHAIRMAN",
            sanctionAdviceReference: "HO-SANCTION-2027-0099");

        facility.RenewalStatus.Should().Be(ContinuousLoanRenewalStatus.Renewed);
        facility.SanctionLimit.Amount.Should().Be(2500000m);
        facility.ExpiryDate.Should().Be(newExpiry);
        facility.RegularInterestRate.Should().Be(9.00m);
        facility.RenewalCount.Should().Be(1);
        facility.IsDebitRestricted.Should().BeFalse();
    }

    // =========================================================================
    // 7. QUARTERLY INTEREST CAPITALIZATION & NPA RESTRICTION (BRPD 3.9.1-i, 3.9.2-ii)
    // =========================================================================

    [Fact]
    public void CapitalizeQuarterlyInterest_StandardAccount_CompoundsInterestIntoPrincipal()
    {
        var facility = CreateSampleFacility(sanctionLimit: 2000000m, initialDp: 2000000m);
        facility.Withdraw(Money.Bdt(1000000m), "CHQ-1", "OFFICER", _today);
        facility.AccrueDailyInterest(_today, days: 90); // ~BDT 23,425 regular interest

        var accruedBefore = facility.RegularAccruedInterest.Amount;
        accruedBefore.Should().BeGreaterThan(0m);

        facility.CapitalizeQuarterlyInterest(new DateOnly(2026, 12, 31));

        facility.OutstandingPrincipal.Amount.Should().Be(1000000m + accruedBefore);
        facility.RegularAccruedInterest.Amount.Should().Be(0m);
    }

    [Theory]
    [InlineData(LoanClassificationGrade.SubStandard_SS)]
    [InlineData(LoanClassificationGrade.Doubtful_DF)]
    [InlineData(LoanClassificationGrade.BadLoss_BL)]
    public void CapitalizeQuarterlyInterest_ClassifiedNpaAccount_ThrowsNpaInterestCapitalizationForbiddenInvariant(
        LoanClassificationGrade npaGrade)
    {
        var facility = CreateSampleFacility(sanctionLimit: 2000000m, initialDp: 2000000m);
        facility.Withdraw(Money.Bdt(1000000m), "CHQ-1", "OFFICER", _today);
        facility.SetClassification(npaGrade, daysPastDue: 200);

        // Interest routes to Interest Suspense on NPA accounts
        facility.AccrueDailyInterest(_today, days: 90);
        facility.InterestSuspense.Amount.Should().BeGreaterThan(0m);

        // Attempting capitalization on NPA is strictly forbidden (BRPD 3.9.2-ii)
        var act = () => facility.CapitalizeQuarterlyInterest(new DateOnly(2026, 12, 31));

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("NpaInterestCapitalizationForbiddenInvariant");
    }

    // =========================================================================
    // HELPER METHODS
    // =========================================================================

    private ContinuousLoanFacility CreateSampleFacility(decimal sanctionLimit, decimal initialDp)
    {
        return new ContinuousLoanFacility(
            accountNumber: _testAccountNumber,
            cif: _testCif,
            branchCode: "BR01",
            category: ContinuousLoanCategory.CashCredit_Hypothecation,
            sanctionLimit: Money.Bdt(sanctionLimit),
            initialDrawingPower: Money.Bdt(initialDp),
            regularInterestRate: 9.50m,
            penalInterestRate: 11.50m,
            sanctionDate: _today,
            expiryDate: _today.AddYears(1),
            stockMarginPercent: 25.00m,
            bookDebtsMarginPercent: 35.00m);
    }
}
