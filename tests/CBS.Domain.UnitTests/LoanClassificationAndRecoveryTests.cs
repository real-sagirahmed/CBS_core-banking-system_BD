// ARCHITECTURAL INTENT: Comprehensive Domain Unit Tests locking Loan Asset Classification (CL),
// Qualitative Judgment (QJ) override invariants, Rescheduling & Restructuring lifecycle guards,
// and Statutory Write-Off & Legal Recovery engine per Bangladesh Bank BRPD Circular No. 12 (13 May 2024)
// Modules 4.1 & 4.2, BRPD 14/2012, 16/2020, 04/2023, 01/2013, 08/2023, and Artha Rin Adalat Ain 2003.

using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class LoanClassificationAndRecoveryTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static LoanAccount CreateDisbursedLoan(
        string? accountNo = null,
        decimal sanctionLimit = 1_000_000m,
        decimal disbursedAmount = 1_000_000m)
    {
        var accNum = accountNo is not null ? AccountNumber.From(accountNo) : AccountNumber.Generate("0101", "200", 1);
        var acc = new LoanAccount(
            accountNumber: accNum,
            cif: new CifNumber("CIF-100001"),
            facilityType: LoanFacilityType.TermLoan,
            branchCode: "0101",
            sanctionLimit: Money.Bdt(sanctionLimit),
            drawingPower: Money.Bdt(sanctionLimit),
            interestRate: 9.00m,
            sanctionDate: Today.AddMonths(-12),
            maturityDate: Today.AddMonths(24),
            sanctionApprovedByUserId: "SANCTION-MGR"
        );

        acc.VetCadChecklist("CAD-OFFICER-01");
        acc.Disburse(Money.Bdt(disbursedAmount), "DISBURSE-OFFICER-01");
        return acc;
    }

    [Theory]
    [InlineData(0, LoanClassificationGrade.Standard)]
    [InlineData(45, LoanClassificationGrade.Standard)]
    [InlineData(60, LoanClassificationGrade.SpecialMentionAccount_SMA)]
    [InlineData(150, LoanClassificationGrade.SpecialMentionAccount_SMA)]
    [InlineData(180, LoanClassificationGrade.SubStandard_SS)]
    [InlineData(250, LoanClassificationGrade.SubStandard_SS)]
    [InlineData(270, LoanClassificationGrade.Doubtful_DF)]
    [InlineData(350, LoanClassificationGrade.Doubtful_DF)]
    [InlineData(360, LoanClassificationGrade.BadLoss_BL)]
    [InlineData(500, LoanClassificationGrade.BadLoss_BL)]
    public void EvaluateClassification_DPDAging_MapsToCorrectRegulatoryGrade(int dpd, LoanClassificationGrade expectedGrade)
    {
        // Arrange
        var loan = CreateDisbursedLoan();

        // Act
        loan.EvaluateClassification(dpd);

        // Assert
        loan.Classification.Should().Be(expectedGrade);
        loan.DaysPastDue.Should().Be(dpd);
    }

    [Fact]
    public void EvaluateClassification_WhenBecomingClassified_TransfersAccruedInterestToSuspense()
    {
        // Arrange
        var loan = CreateDisbursedLoan();
        loan.AccrueDailyInterest(180); // Accrues interest
        loan.RegularAccruedInterest.Amount.Should().BeGreaterThan(0);
        var accruedAmount = loan.RegularAccruedInterest.Amount;

        // Act: Transition from Standard to SubStandard_SS (DPD >= 180)
        loan.EvaluateClassification(185);

        // Assert
        loan.Classification.Should().Be(LoanClassificationGrade.SubStandard_SS);
        loan.RegularAccruedInterest.Amount.Should().Be(0m);
        loan.InterestSuspense.Amount.Should().Be(accruedAmount);
    }

    [Fact]
    public void StatutoryProvisioning_CalculatesBaseAndProvisionCorrectly()
    {
        // Arrange: Loan 1,000,000 BDT, Collateral FSV 400,000 BDT, Interest Suspense 100,000 BDT
        var loan = CreateDisbursedLoan();
        loan.SetEligibleCollateralFsv(Money.Bdt(400_000m));
        loan.EvaluateClassification(370); // Bad/Loss (BL)

        // Act
        // Base for Provision = Max(0, 1,000,000 - 400,000 - 0) = 600,000 BDT
        var provBase = loan.CalculateProvisionBase();
        loan.CalculateProvisionRequirement(generalProvisionRate: 1.0m, specificProvisionRate: 100.0m);

        // Assert
        provBase.Amount.Should().Be(600_000m);
        loan.RequiredGeneralProvision.Amount.Should().Be(0m);
        loan.RequiredSpecificProvision.Amount.Should().Be(600_000m);
    }

    [Fact]
    public void QualitativeJudgment_DowngradeOnly_RejectsUpgradeAttempt()
    {
        // Arrange
        var loan = CreateDisbursedLoan();
        loan.EvaluateClassification(200); // SubStandard_SS

        // Act & Assert: QJ cannot upgrade SS to Standard
        var act = () => loan.ApplyQualitativeJudgment(
            grade: LoanClassificationGrade.Standard,
            reason: QualitativeJudgmentReason.CentralBankInspectionFinding,
            justification: "Requesting upgrade",
            inspectorId: "INSPECTOR-01",
            checkerId: "CHECKER-01"
        );

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Qualitative Judgment (QJ) can only be applied for non-performing classification*");
    }

    [Fact]
    public void QualitativeJudgment_MakerCheckerSeparation_ThrowsWhenInspectorEqualsChecker()
    {
        // Arrange
        var loan = CreateDisbursedLoan();

        // Act & Assert
        var act = () => loan.ApplyQualitativeJudgment(
            grade: LoanClassificationGrade.Doubtful_DF,
            reason: QualitativeJudgmentReason.FraudOrForgery,
            justification: "Suspected forged documents found during audit",
            inspectorId: "AUDITOR-SAME",
            checkerId: "AUDITOR-SAME"
        );

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Inspector cannot act as Checker*");
    }

    [Fact]
    public void QualitativeJudgment_PreventsAutoUpgradeAtEodDpdAging()
    {
        // Arrange: Loan has DPD 20 (Standard), but auditor applies Qualitative Judgment -> Doubtful_DF
        var loan = CreateDisbursedLoan();
        loan.EvaluateClassification(20);
        loan.Classification.Should().Be(LoanClassificationGrade.Standard);

        loan.ApplyQualitativeJudgment(
            grade: LoanClassificationGrade.Doubtful_DF,
            reason: QualitativeJudgmentReason.BusinessSuspensionOrClosure,
            justification: "Factory shut down indefinitely",
            inspectorId: "INSPECTOR-01",
            checkerId: "CHECKER-01"
        );

        loan.Classification.Should().Be(LoanClassificationGrade.Doubtful_DF);
        loan.HasQualitativeJudgment.Should().BeTrue();

        // Act: EOD batch runs DPD aging with DPD = 10 (which objectively is Standard)
        loan.EvaluateClassification(10);

        // Assert: Locked at Doubtful_DF! (Rule 5.2)
        loan.Classification.Should().Be(LoanClassificationGrade.Doubtful_DF);
    }

    [Fact]
    public void QualitativeJudgment_RevocationWithDualControl_RestoresObjectiveGrade()
    {
        // Arrange
        var loan = CreateDisbursedLoan();
        loan.EvaluateClassification(15);
        loan.ApplyQualitativeJudgment(
            grade: LoanClassificationGrade.SubStandard_SS,
            reason: QualitativeJudgmentReason.LitigationAdverseCourtOrder,
            justification: "Pending litigation",
            inspectorId: "INSPECTOR-01",
            checkerId: "CHECKER-01"
        );
        loan.Classification.Should().Be(LoanClassificationGrade.SubStandard_SS);

        // Act: Committee revokes QJ
        loan.RevokeQualitativeJudgment(
            justification: "Court injunction lifted and borrower cleared",
            inspectorId: "INSPECTOR-01",
            checkerId: "CHECKER-02"
        );

        // Assert: Restored to objective Standard (DPD = 15)
        loan.HasQualitativeJudgment.Should().BeFalse();
        loan.Classification.Should().Be(LoanClassificationGrade.Standard);
    }

    [Fact]
    public void Reschedule_ThrowsWhenDownPaymentInsufficient()
    {
        // Arrange
        var loan = CreateDisbursedLoan();
        loan.EvaluateClassification(120);

        // Act & Assert: Required 150k BDT, paid only 100k BDT
        var act = () => loan.Reschedule(
            newRescheduleIteration: 1,
            downPaymentPaid: Money.Bdt(100_000m),
            requiredDownPayment: Money.Bdt(150_000m),
            newMaturityDate: Today.AddMonths(36),
            newInterestRate: 9.50m,
            boardResolutionRef: "",
            boardApprovalDate: Today,
            interestWaiver: null,
            makerUserId: "MAKER-01",
            checkerUserId: "CHECKER-01"
        );

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*less than statutory mandatory down payment*");
    }

    [Fact]
    public void Reschedule_ThrowsWhenExceedingMax3Iterations()
    {
        // Arrange
        var loan = CreateDisbursedLoan();

        // Act & Assert: Iteration 4 is strictly prohibited
        var act = () => loan.Reschedule(
            newRescheduleIteration: 4,
            downPaymentPaid: Money.Bdt(200_000m),
            requiredDownPayment: Money.Bdt(200_000m),
            newMaturityDate: Today.AddMonths(36),
            newInterestRate: 9.50m,
            boardResolutionRef: "BOD-RES-999",
            boardApprovalDate: Today,
            interestWaiver: null,
            makerUserId: "MAKER-01",
            checkerUserId: "CHECKER-01"
        );

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*reached the maximum permitted 3 reschedulings*");
    }

    [Fact]
    public void Reschedule_ThrowsWhenBoardApprovalMissingForIteration2OrMore()
    {
        // Arrange
        var loan = CreateDisbursedLoan();

        // Act & Assert: Iteration 2 without board resolution
        var act = () => loan.Reschedule(
            newRescheduleIteration: 2,
            downPaymentPaid: Money.Bdt(150_000m),
            requiredDownPayment: Money.Bdt(150_000m),
            newMaturityDate: Today.AddMonths(36),
            newInterestRate: 9.50m,
            boardResolutionRef: "",
            boardApprovalDate: Today,
            interestWaiver: null,
            makerUserId: "MAKER-01",
            checkerUserId: "CHECKER-01"
        );

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*requires mandatory Board of Directors approval resolution reference*");
    }

    [Fact]
    public void Reschedule_Successful_UpdatesTermsResetsDpdAndReducesPrincipal()
    {
        // Arrange
        var loan = CreateDisbursedLoan(disbursedAmount: 1_000_000m);
        loan.EvaluateClassification(150); // SMA
        var newMaturity = loan.MaturityDate.AddMonths(12);

        // Act
        loan.Reschedule(
            newRescheduleIteration: 1,
            downPaymentPaid: Money.Bdt(150_000m),
            requiredDownPayment: Money.Bdt(150_000m),
            newMaturityDate: newMaturity,
            newInterestRate: 9.25m,
            boardResolutionRef: "MD-APPROVAL-001",
            boardApprovalDate: Today,
            interestWaiver: null,
            makerUserId: "MAKER-01",
            checkerUserId: "CHECKER-01"
        );

        // Assert
        loan.RescheduleCount.Should().Be(1);
        loan.OutstandingPrincipal.Amount.Should().Be(850_000m); // 1,000,000 - 150,000
        loan.DaysPastDue.Should().Be(0);
        loan.Classification.Should().Be(LoanClassificationGrade.Standard);
        loan.LoanStatus.Should().Be(LoanAccountStatus.Rescheduled);
        loan.MaturityDate.Should().Be(newMaturity);
        loan.InterestRate.Should().Be(9.25m);
    }

    [Fact]
    public void WriteOff_RejectsNonBadLossFacility()
    {
        // Arrange
        var loan = CreateDisbursedLoan();
        loan.EvaluateClassification(200); // SubStandard_SS

        // Act & Assert
        var act = () => loan.WriteOff(
            specificProvisionAvailable: Money.Bdt(1_000_000m),
            arthaRinCaseNumber: "ARA-101/2026",
            arthaRinFilingDate: Today,
            boardResolutionNumber: "BOD-WO-2026-01",
            boardApprovalDate: Today,
            makerUserId: "MAKER-01",
            checkerUserId: "CHECKER-01"
        );

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Facility must be classified as Bad/Loss (BL)*");
    }

    [Fact]
    public void WriteOff_RejectsWhenSpecificProvisionLessThanPrincipal()
    {
        // Arrange: 1,000,000 BDT outstanding, but provision is only 800,000 BDT
        var loan = CreateDisbursedLoan();
        loan.EvaluateClassification(400); // BadLoss_BL

        // Act & Assert
        var act = () => loan.WriteOff(
            specificProvisionAvailable: Money.Bdt(800_000m),
            arthaRinCaseNumber: "ARA-101/2026",
            arthaRinFilingDate: Today,
            boardResolutionNumber: "BOD-WO-2026-01",
            boardApprovalDate: Today,
            makerUserId: "MAKER-01",
            checkerUserId: "CHECKER-01"
        );

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*100% specific provision required*");
    }

    [Fact]
    public void WriteOff_RejectsOver500kWithoutArthaRinSuitFiling()
    {
        // Arrange: 1,000,000 BDT outstanding, 100% provision, but court case number missing
        var loan = CreateDisbursedLoan();
        loan.EvaluateClassification(400); // BadLoss_BL

        // Act & Assert
        var act = () => loan.WriteOff(
            specificProvisionAvailable: Money.Bdt(1_000_000m),
            arthaRinCaseNumber: "",
            arthaRinFilingDate: null,
            boardResolutionNumber: "BOD-WO-2026-01",
            boardApprovalDate: Today,
            makerUserId: "MAKER-01",
            checkerUserId: "CHECKER-01"
        );

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*without filing suit in Artha Rin Adalat*");
    }

    [Fact]
    public void WriteOff_Successful_And_PostWriteOffRecoveryWaterfall()
    {
        // Arrange
        var loan = CreateDisbursedLoan(disbursedAmount: 600_000m);
        loan.EvaluateClassification(400); // BadLoss_BL

        // Act: Execute Write-off
        loan.WriteOff(
            specificProvisionAvailable: Money.Bdt(600_000m),
            arthaRinCaseNumber: "ARA-502/2026",
            arthaRinFilingDate: Today,
            boardResolutionNumber: "BOD-WO-2026-99",
            boardApprovalDate: Today,
            makerUserId: "MAKER-01",
            checkerUserId: "CHECKER-01"
        );

        // Assert Write-off
        loan.IsWrittenOff.Should().BeTrue();
        loan.LoanStatus.Should().Be(LoanAccountStatus.WrittenOff);

        // Act: Recovery of 200,000 BDT cash
        loan.RecoverWrittenOff(
            Money.Bdt(200_000m),
            out var allocPrincipal,
            out var allocInterest
        );

        // Assert Recovery Waterfall
        allocPrincipal.Amount.Should().Be(200_000m);
        allocInterest.Amount.Should().Be(0m);
        loan.OutstandingPrincipal.Amount.Should().Be(400_000m); // 600k - 200k

        // Act: Recovery of remaining 400,000 BDT + 50,000 BDT interest
        loan.RecoverWrittenOff(
            Money.Bdt(450_000m),
            out var finalPrincipal,
            out var finalInterest
        );

        // Assert final full settlement
        finalPrincipal.Amount.Should().Be(400_000m);
        finalInterest.Amount.Should().Be(50_000m);
        loan.OutstandingPrincipal.Amount.Should().Be(0m);
        loan.LoanStatus.Should().Be(LoanAccountStatus.Closed);
    }
}
