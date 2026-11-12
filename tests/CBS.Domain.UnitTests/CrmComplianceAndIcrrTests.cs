// ARCHITECTURAL INTENT: Comprehensive Domain Unit Tests locking Customer Risk Grading (CRG),
// Real-Time Sanction Screening, FATCA US Indicia Detection, and Borrower ICRR Scorecard
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 8, BRPD 03/2019, and e-KYC Guidelines.

using CBS.Domain.Common;
using CBS.Domain.Customers;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class CrmComplianceAndIcrrTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    // ==========================================
    // 1. CUSTOMER RISK GRADING (CRG) TESTS
    // ==========================================

    [Fact]
    public void CustomerRiskAssessment_LowRisk_Should_Assign_Grade_And_24Month_Review()
    {
        // ARCHITECTURAL INTENT: Low risk customers have 24-month CDD review cycle [BRPD 8.1.13, e-KYC]
        var assessment = new CustomerRiskAssessment(
            id: Guid.NewGuid(),
            cif: new CifNumber("CIF-100001"),
            channel: OnboardingChannel.BiometricEkyc, // 1 pt
            isBorderOrHighRiskArea: false,            // 1 pt
            isPoliticallyExposedPerson: false,        // 0 pt
            isInfluentialPersonOrAssociate: false,    // 0 pt
            productRiskScore: 1,                      // 1 pt (Savings)
            occupationRiskScore: 1,                   // 1 pt (Salaried)
            assessmentDate: Today,
            evaluatedByUserId: "OFFICER-01"
        );

        assessment.CompositeRiskScore.Should().Be(4); // 1 + 1 + 0 + 1 + 1
        assessment.RiskGrade.Should().Be(CustomerRiskGrade.Low);
        assessment.IsEnhancedDueDiligenceRequired.Should().BeFalse();
        assessment.NextReviewDate.Should().Be(Today.AddYears(2));
    }

    [Fact]
    public void CustomerRiskAssessment_PepOrInfluentialPerson_Should_Enforce_HighRisk_And_SeniorMgmtApproval()
    {
        // ARCHITECTURAL INTENT: PEP or IP automatically triggers High Risk, mandatory EDD,
        // and senior management approval [BRPD 8.1.13, e-KYC Guidelines]
        var assessment = new CustomerRiskAssessment(
            id: Guid.NewGuid(),
            cif: new CifNumber("CIF-100002"),
            channel: OnboardingChannel.BranchPhysical,
            isBorderOrHighRiskArea: false,
            isPoliticallyExposedPerson: true,
            isInfluentialPersonOrAssociate: false,
            productRiskScore: 3,
            occupationRiskScore: 5,
            assessmentDate: Today,
            evaluatedByUserId: "OFFICER-01"
        );

        assessment.RiskGrade.Should().Be(CustomerRiskGrade.High);
        assessment.IsEnhancedDueDiligenceRequired.Should().BeTrue();
        assessment.NextReviewDate.Should().Be(Today.AddMonths(6)); // 6-month review cycle

        // Unapproved initially
        assessment.SeniorManagementApprovedByUserId.Should().BeNull();

        // Senior Management approves onboarding
        assessment.ApproveSeniorManagement("MANAGING-DIRECTOR-01");
        assessment.SeniorManagementApprovedByUserId.Should().Be("MANAGING-DIRECTOR-01");
        assessment.SeniorManagementApprovedAtUtc.Should().NotBeNull();
    }

    // ==========================================
    // 2. REAL-TIME SANCTION SCREENING TESTS
    // ==========================================

    [Fact]
    public void SanctionScreening_PotentialMatch_Should_HardBlock_And_Require_DualControl()
    {
        // ARCHITECTURAL INTENT: Sanction potential hit triggers immediate regulatory hard block
        // and requires dual-control investigation [BRPD 8.1.14, AML Act 2012]
        var screening = new SanctionScreeningRecord(
            id: Guid.NewGuid(),
            fullName: "VIKTOR BOUT",
            dateOfBirth: new DateOnly(1967, 1, 13),
            nationality: "RUSSIAN"
        );

        screening.FlagPotentialMatch(
            watchlistType: SanctionWatchlistType.Unscr1267,
            matchedEntityName: "VIKTOR BOUT",
            matchedEntityReference: "UNSCR-1521-001",
            matchConfidenceScore: 100m
        );

        screening.Status.Should().Be(SanctionMatchStatus.PotentialMatch);
        screening.IsHardBlocked.Should().BeTrue();

        // Compliance Officer (Maker) investigates
        screening.Investigate(
            complianceOfficerUserId: "COMPLIANCE-MAKER-01",
            investigationNotes: "Passport and biographical data match UN 1267 designated arms trafficker.",
            isPotentialTruePositive: true
        );

        // Invariant: Maker cannot approve own investigation resolution
        var selfApprove = () => screening.ConfirmResolutionByComplianceHead(
            complianceHeadUserId: "COMPLIANCE-MAKER-01",
            confirmAsSanctioned: true
        );
        selfApprove.Should().Throw<InvariantViolationException>()
            .WithMessage("*Compliance Officer (Maker) cannot approve own sanction investigation*");

        // Independent Checker (Compliance Head) confirms sanction hit
        screening.ConfirmResolutionByComplianceHead(
            complianceHeadUserId: "COMPLIANCE-HEAD-01",
            confirmAsSanctioned: true
        );

        screening.Status.Should().Be(SanctionMatchStatus.ConfirmedMatch);
        screening.IsHardBlocked.Should().BeTrue();
    }

    [Fact]
    public void SanctionScreening_FalsePositive_Resolution_Should_Clear_HardBlock()
    {
        // ARCHITECTURAL INTENT: Verified false positive clears hard block with audit notes
        var screening = new SanctionScreeningRecord(
            id: Guid.NewGuid(),
            fullName: "MD ALI HOSSAIN",
            dateOfBirth: new DateOnly(1990, 5, 10),
            nationality: "BANGLADESHI"
        );

        screening.FlagPotentialMatch(
            watchlistType: SanctionWatchlistType.BfiuDomestic,
            matchedEntityName: "ALI HUSSEIN",
            matchedEntityReference: "BFIU-DOM-009",
            matchConfidenceScore: 85m
        );

        screening.IsHardBlocked.Should().BeTrue();

        screening.Investigate(
            complianceOfficerUserId: "COMPLIANCE-MAKER-01",
            investigationNotes: "Customer NID, father name, and DOB completely distinct from designated individual.",
            isPotentialTruePositive: false
        );

        screening.ConfirmResolutionByComplianceHead(
            complianceHeadUserId: "COMPLIANCE-HEAD-01",
            confirmAsSanctioned: false
        );

        screening.Status.Should().Be(SanctionMatchStatus.FalsePositive);
        screening.IsHardBlocked.Should().BeFalse();
    }

    // ==========================================
    // 3. FATCA US INDICIA DETECTION TESTS
    // ==========================================

    [Fact]
    public void FatcaProfile_UsIndicia_Should_Require_Cure_And_Become_Recalcitrant_After_90Days()
    {
        // ARCHITECTURAL INTENT: US indicia requires Form W-9 / W-8BEN cure within 90 days,
        // otherwise customer becomes Recalcitrant subject to 30% withholding [BRPD 8.1.15, US FATCA]
        var profile = new FatcaComplianceProfile(
            id: Guid.NewGuid(),
            cif: new CifNumber("CIF-100003"),
            hasUsCitizenshipOrGreenCard: false,
            hasUsPlaceOfBirth: true, // Born in New York
            hasUsAddress: false,
            hasUsPhoneNumber: false,
            hasStandingInstructionsToUsAccount: false,
            hasPowerOfAttorneyGrantedToUsAddress: false,
            hasInCareOfOrHoldMailAddress: false,
            evaluationDate: Today.AddDays(-100), // Evaluated 100 days ago
            evaluatedByUserId: "KYC-OFFICER-01"
        );

        profile.HasAnyUsIndicia.Should().BeTrue();
        profile.IsSubjectToWithholdingTax.Should().BeFalse();

        // 100 days elapsed > 90-day cure deadline
        profile.CheckAndEnforceRecalcitrantStatus(Today);

        profile.Classification.Should().Be(FatcaClassification.RecalcitrantAccountHolder);
        profile.IsSubjectToWithholdingTax.Should().BeTrue();
    }

    [Fact]
    public void FatcaProfile_ValidW9_Should_Set_UsPersonCompliant()
    {
        var profile = new FatcaComplianceProfile(
            id: Guid.NewGuid(),
            cif: new CifNumber("CIF-100004"),
            hasUsCitizenshipOrGreenCard: true,
            hasUsPlaceOfBirth: false,
            hasUsAddress: true,
            hasUsPhoneNumber: true,
            hasStandingInstructionsToUsAccount: false,
            hasPowerOfAttorneyGrantedToUsAddress: false,
            hasInCareOfOrHoldMailAddress: false,
            evaluationDate: Today,
            evaluatedByUserId: "KYC-OFFICER-01"
        );

        // Submit Form W-9 with valid 9-digit SSN
        profile.SubmitFormW9("123-45-6789", Today, "FATCA-OFFICER-01");

        profile.Classification.Should().Be(FatcaClassification.UsPersonCompliant);
        profile.IsFormW9Submitted.Should().BeTrue();
        profile.IsSubjectToWithholdingTax.Should().BeFalse();

        // Invalid TIN throws InvariantViolationException
        var act = () => profile.SubmitFormW9("INVALID-TIN", Today, "FATCA-OFFICER-01");
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*US Taxpayer Identification Number must be a valid 9-digit number*");
    }

    // ==========================================
    // 4. BORROWER ICRR SCORECARD TESTS
    // ==========================================

    [Theory]
    [InlineData(14, 14, 14, 14, 10, 10, 8, IcrrGrade.Excellent)]   // 56 + 28 = 84 (>=75)
    [InlineData(12, 12, 11, 11, 8, 8, 6, IcrrGrade.Good)]          // 46 + 22 = 68 (65-74)
    [InlineData(10, 10, 10, 10, 6, 6, 5, IcrrGrade.Marginal)]      // 40 + 17 = 57 (55-64)
    [InlineData(6, 6, 6, 6, 4, 4, 3, IcrrGrade.Unacceptable)]      // 24 + 11 = 35 (<55)
    public void BorrowerIcrrAssessment_Should_Accurately_Classify_Grade(
        decimal lev, decimal liq, decimal prof, decimal cov,
        decimal bus, decimal mgmt, decimal sec,
        IcrrGrade expectedGrade)
    {
        // ARCHITECTURAL INTENT: Standardized 60% Quantitative + 40% Qualitative Scorecard [BRPD Circular 03/2019]
        var assessment = new BorrowerIcrrAssessment(
            id: Guid.NewGuid(),
            cif: new CifNumber("CIF-200001"),
            borrowerName: "BEXIMCO PHARMA LTD",
            industrySector: "Pharmaceuticals",
            financialStatementDate: Today.AddMonths(-6),
            assessmentDate: Today,
            leverageScore: lev,
            liquidityScore: liq,
            profitabilityScore: prof,
            coverageScore: cov,
            businessRiskScore: bus,
            managementRiskScore: mgmt,
            securityRiskScore: sec,
            evaluatedByUserId: "CREDIT-ANALYST-01"
        );

        assessment.RatingGrade.Should().Be(expectedGrade);
    }

    [Fact]
    public void BorrowerIcrrAssessment_Unacceptable_Should_Block_Sanction_Until_BoardApproval()
    {
        // ARCHITECTURAL INTENT: Borrower with Unacceptable ICRR score (<55) cannot receive credit
        // without explicit Board of Directors approval [BRPD Circular 03/2019 §4.3]
        var assessment = new BorrowerIcrrAssessment(
            id: Guid.NewGuid(),
            cif: new CifNumber("CIF-200002"),
            borrowerName: "STRUGGLING TEXTILES LTD",
            industrySector: "Textiles",
            financialStatementDate: Today.AddMonths(-6),
            assessmentDate: Today,
            leverageScore: 5,
            liquidityScore: 5,
            profitabilityScore: 5,
            coverageScore: 5,
            businessRiskScore: 5,
            managementRiskScore: 5,
            securityRiskScore: 5,
            evaluatedByUserId: "CREDIT-ANALYST-01"
        );

        assessment.RatingGrade.Should().Be(IcrrGrade.Unacceptable);
        assessment.IsBoardOfDirectorsApproved.Should().BeFalse();

        // Must throw invariant without board approval
        var act = () => assessment.VerifyCreditSanctionPermitted();
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Credit sanction is strictly prohibited without explicit Board of Directors approval*");

        // Board of Directors formally approves credit with mitigation strategy
        assessment.ApproveByBoardOfDirectors(
            boardResolutionReference: "BOD-RES-2026-8891",
            boardResolutionDate: Today,
            boardMitigationStrategy: "Additional 150% prime land mortgage collateral perfected.",
            approvedByUserId: "COMPANY-SECRETARY"
        );

        assessment.IsBoardOfDirectorsApproved.Should().BeTrue();
        // Verification should now succeed without throwing
        var permittedAct = () => assessment.VerifyCreditSanctionPermitted();
        permittedAct.Should().NotThrow();
    }
}
