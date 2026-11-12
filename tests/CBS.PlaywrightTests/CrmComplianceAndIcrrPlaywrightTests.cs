// ARCHITECTURAL INTENT: End-to-End Playwright API Integration tests locking
// Customer Risk Grading (CRG), Real-Time Sanction Screening, FATCA US Indicia Detection,
// and Internal Credit Risk Rating System (ICRR) per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 8,
// BRPD Circular No. 03/2019, Money Laundering Prevention Act 2012, and US IRC Chapter 4.

using System.Text.Json;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class CrmComplianceAndIcrrPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public CrmComplianceAndIcrrPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CustomerRiskGrading_Should_Calculate_Grade_And_Enforce_Senior_Management_Approval_For_Pep()
    {
        // ARCHITECTURAL INTENT: Customer Risk Grading multi-factor engine must enforce 6-month review
        // and senior management approval for High Risk/PEP entities [BRPD 8.1.13; e-KYC Guidelines]
        var api = await _fixture.CreateApiContextAsync();
        var lowRiskCif = $"CIF-LOW-{Guid.NewGuid():N}"[..18];
        var pepCif = $"CIF-PEP-{Guid.NewGuid():N}"[..18];

        // 1. Evaluate Low Risk Customer (Score < 11)
        var lowCmd = new EvaluateCustomerRiskCommand(
            Cif: lowRiskCif,
            Channel: OnboardingChannel.BranchPhysical,
            IsBorderOrHighRiskArea: false,
            IsPoliticallyExposedPerson: false,
            IsInfluentialPersonOrAssociate: false,
            ProductRiskScore: 1,
            OccupationRiskScore: 1,
            AssessmentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EvaluatedByUserId: "OFFICER-01"
        );

        var lowRes = await api.PostAsync("/api/v1/crm/compliance/risk/evaluate", new APIRequestContextOptions
        {
            DataObject = lowCmd
        });
        lowRes.Status.Should().Be(200);

        var lowAssessment = (await lowRes.JsonAsync())?.Deserialize<CustomerRiskAssessmentDto>(JsonOptions);
        lowAssessment.Should().NotBeNull();
        lowAssessment!.RiskGrade.Should().Be(CustomerRiskGrade.Low);
        lowAssessment.IsEnhancedDueDiligenceRequired.Should().BeFalse();
        lowAssessment.IsApproved.Should().BeFalse();
        lowAssessment.SeniorManagementApprovedByUserId.Should().BeNull();

        // 2. Evaluate High Risk PEP Customer
        var pepCmd = new EvaluateCustomerRiskCommand(
            Cif: pepCif,
            Channel: OnboardingChannel.DigitalApp,
            IsBorderOrHighRiskArea: true,
            IsPoliticallyExposedPerson: true,
            IsInfluentialPersonOrAssociate: true,
            ProductRiskScore: 5,
            OccupationRiskScore: 5,
            AssessmentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EvaluatedByUserId: "OFFICER-01"
        );

        var pepRes = await api.PostAsync("/api/v1/crm/compliance/risk/evaluate", new APIRequestContextOptions
        {
            DataObject = pepCmd
        });
        pepRes.Status.Should().Be(200);

        var pepAssessment = (await pepRes.JsonAsync())?.Deserialize<CustomerRiskAssessmentDto>(JsonOptions);
        pepAssessment.Should().NotBeNull();
        pepAssessment!.RiskGrade.Should().Be(CustomerRiskGrade.High);
        pepAssessment.IsEnhancedDueDiligenceRequired.Should().BeTrue();
        pepAssessment.IsApproved.Should().BeFalse();

        // 3. Senior Management Approves PEP
        var approveCmd = new ApproveSeniorManagementCommand(
            Cif: pepCif,
            SeniorManagerUserId: "MANAGING-DIRECTOR-01"
        );

        var approveRes = await api.PostAsync("/api/v1/crm/compliance/risk/approve-senior-mgmt", new APIRequestContextOptions
        {
            DataObject = approveCmd
        });
        approveRes.Status.Should().Be(200);

        // 4. Verify via GET
        var getRes = await api.GetAsync($"/api/v1/crm/compliance/risk/{pepCif}");
        getRes.Status.Should().Be(200);
        var refreshed = (await getRes.JsonAsync())?.Deserialize<CustomerRiskAssessmentDto>(JsonOptions);
        refreshed.Should().NotBeNull();
        refreshed!.IsApproved.Should().BeTrue();
        refreshed.SeniorManagementApprovedByUserId.Should().Be("MANAGING-DIRECTOR-01");
    }

    [Fact]
    public async Task SanctionScreening_Should_Detect_Fuzzy_Match_And_Enforce_Maker_Checker_Dual_Control()
    {
        // ARCHITECTURAL INTENT: Real-time screening against UNSCR/OFAC lists blocks potential hits
        // and requires strict Maker-Checker dual control for resolution [BRPD 8.1.14; MLPA 2012]
        var api = await _fixture.CreateApiContextAsync();
        var cleanCif = $"CIF-SAN-CLR-{Guid.NewGuid():N}"[..18];
        var hitCif = $"CIF-SAN-HIT-{Guid.NewGuid():N}"[..18];

        // 1. Screen Clean Name
        var cleanCmd = new ScreenSanctionCommand(
            Cif: cleanCif,
            FullName: "Anisur Rahman Chowdhury",
            DateOfBirth: new DateOnly(1985, 3, 15),
            Nationality: "Bangladeshi"
        );

        var cleanRes = await api.PostAsync("/api/v1/crm/compliance/sanctions/screen", new APIRequestContextOptions
        {
            DataObject = cleanCmd
        });
        cleanRes.Status.Should().Be(200);

        var cleanRecord = (await cleanRes.JsonAsync())?.Deserialize<SanctionScreeningResultDto>(JsonOptions);
        cleanRecord.Should().NotBeNull();
        cleanRecord!.Status.Should().Be(SanctionMatchStatus.Clear);
        cleanRecord.IsHardBlocked.Should().BeFalse();

        // 2. Screen Watchlist Match ("Viktor Bout")
        var hitCmd = new ScreenSanctionCommand(
            Cif: hitCif,
            FullName: "Viktor Bout",
            DateOfBirth: new DateOnly(1967, 1, 13),
            Nationality: "Russian"
        );

        var hitRes = await api.PostAsync("/api/v1/crm/compliance/sanctions/screen", new APIRequestContextOptions
        {
            DataObject = hitCmd
        });
        hitRes.Status.Should().Be(200);

        var hitRecord = (await hitRes.JsonAsync())?.Deserialize<SanctionScreeningResultDto>(JsonOptions);
        hitRecord.Should().NotBeNull();
        hitRecord!.Status.Should().Be(SanctionMatchStatus.PotentialMatch);
        hitRecord.IsHardBlocked.Should().BeTrue();
        hitRecord.WatchlistType.Should().Be(SanctionWatchlistType.Unscr1267);
        hitRecord.MatchConfidenceScore.Should().BeGreaterThan(60m);

        // 3. Maker Investigates (False Positive rationale)
        var investCmd = new InvestigateSanctionCommand(
            ScreeningId: hitRecord.ScreeningId,
            ComplianceOfficerUserId: "COMPLIANCE-ANALYST-01",
            InvestigationNotes: "Verified customer passport; individual has different middle name and mother details. False positive.",
            IsPotentialTruePositive: false
        );

        var investRes = await api.PostAsync("/api/v1/crm/compliance/sanctions/investigate", new APIRequestContextOptions
        {
            DataObject = investCmd
        });
        investRes.Status.Should().Be(200);

        // 4. Maker Attempts to Check/Resolve Own Investigation -> Fails with MakerCheckerSeparationInvariant
        var selfResolveCmd = new ResolveSanctionCheckerCommand(
            ScreeningId: hitRecord.ScreeningId,
            ComplianceHeadUserId: "COMPLIANCE-ANALYST-01", // Violates 4-eyes principle
            ConfirmAsSanctioned: false
        );

        var selfResolveRes = await api.PostAsync("/api/v1/crm/compliance/sanctions/resolve", new APIRequestContextOptions
        {
            DataObject = selfResolveCmd
        });
        selfResolveRes.Status.Should().Be(400); // Invariant violation

        // 5. Independent Checker Resolves
        var validResolveCmd = new ResolveSanctionCheckerCommand(
            ScreeningId: hitRecord.ScreeningId,
            ComplianceHeadUserId: "CAMLCO-HEAD-OF-COMPLIANCE",
            ConfirmAsSanctioned: false
        );

        var resolveRes = await api.PostAsync("/api/v1/crm/compliance/sanctions/resolve", new APIRequestContextOptions
        {
            DataObject = validResolveCmd
        });
        resolveRes.Status.Should().Be(200);

        var resolvedRecord = (await resolveRes.JsonAsync())?.Deserialize<SanctionScreeningResultDto>(JsonOptions);
        resolvedRecord.Should().NotBeNull();
        resolvedRecord!.Status.Should().Be(SanctionMatchStatus.FalsePositive);
        resolvedRecord.IsHardBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task FatcaCompliance_Should_Identify_Us_Indicia_And_Validate_Form_W9()
    {
        // ARCHITECTURAL INTENT: FATCA US indicia tracking enforces 90-day cure window,
        // validates Form W-9 TIN format, and marks compliant vs recalcitrant [BRPD 8.1.15; US IRC Chapter 4]
        var api = await _fixture.CreateApiContextAsync();
        var fatcaCif = $"CIF-FATCA-{Guid.NewGuid():N}"[..18];

        // 1. Evaluate FATCA with US Indicia (Born in USA)
        var evaluateCmd = new EvaluateFatcaIndiciaCommand(
            Cif: fatcaCif,
            HasUsCitizenshipOrGreenCard: true,
            HasUsPlaceOfBirth: true,
            HasUsAddress: true,
            HasUsPhoneNumber: true,
            HasStandingInstructionsToUsAccount: false,
            HasPowerOfAttorneyGrantedToUsAddress: false,
            HasInCareOfOrHoldMailAddress: false,
            EvaluationDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EvaluatedByUserId: "OFFICER-01"
        );

        var evalRes = await api.PostAsync("/api/v1/crm/compliance/fatca/evaluate", new APIRequestContextOptions
        {
            DataObject = evaluateCmd
        });
        evalRes.Status.Should().Be(200);

        var profile = (await evalRes.JsonAsync())?.Deserialize<FatcaComplianceProfileDto>(JsonOptions);
        profile.Should().NotBeNull();
        profile!.HasAnyUsIndicia.Should().BeTrue();
        profile.CureDeadlineDate.Should().NotBeNull();
        profile.Classification.Should().Be(FatcaClassification.NonUsPerson);

        // 2. Submit Invalid W-9 (TIN must be 9 digits)
        var invalidW9Cmd = new SubmitFatcaW9Command(
            Cif: fatcaCif,
            UsTin: "12345", // Only 5 digits
            SubmissionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            VerifiedByUserId: "COMPLIANCE-01"
        );

        var invalidRes = await api.PostAsync("/api/v1/crm/compliance/fatca/w9", new APIRequestContextOptions
        {
            DataObject = invalidW9Cmd
        });
        invalidRes.Status.Should().Be(400);

        // 3. Submit Valid W-9 (9 digits)
        var validW9Cmd = new SubmitFatcaW9Command(
            Cif: fatcaCif,
            UsTin: "123456789",
            SubmissionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            VerifiedByUserId: "COMPLIANCE-01"
        );

        var validRes = await api.PostAsync("/api/v1/crm/compliance/fatca/w9", new APIRequestContextOptions
        {
            DataObject = validW9Cmd
        });
        validRes.Status.Should().Be(200);

        var updatedProfile = (await validRes.JsonAsync())?.Deserialize<FatcaComplianceProfileDto>(JsonOptions);
        updatedProfile.Should().NotBeNull();
        updatedProfile!.Classification.Should().Be(FatcaClassification.UsPersonCompliant);
        updatedProfile.IsSubjectToWithholdingTax.Should().BeFalse();
        updatedProfile.UsTaxpayerIdentificationNumber.Should().Be("123456789");

        // 4. Verify via GET endpoint
        var getRes = await api.GetAsync($"/api/v1/crm/compliance/fatca/{fatcaCif}");
        getRes.Status.Should().Be(200);
        var fetched = (await getRes.JsonAsync())?.Deserialize<FatcaComplianceProfileDto>(JsonOptions);
        fetched.Should().NotBeNull();
        fetched!.Classification.Should().Be(FatcaClassification.UsPersonCompliant);
    }

    [Fact]
    public async Task BorrowerIcrr_Should_Block_Unacceptable_Borrower_Until_Board_Approval()
    {
        // ARCHITECTURAL INTENT: Borrower ICRR enforces strict prohibition against fresh credit sanctions
        // for Unacceptable borrowers (<55 points) unless approved by the Board of Directors [BRPD 8.1.16, 8.1.17; BRPD 03/2019]
        var api = await _fixture.CreateApiContextAsync();
        var excellentCif = $"CIF-ICRR-EXC-{Guid.NewGuid():N}"[..18];
        var unaccCif = $"CIF-ICRR-UNA-{Guid.NewGuid():N}"[..18];

        // 1. Evaluate Excellent Borrower (Total = 91 points)
        var excellentCmd = new EvaluateBorrowerIcrrCommand(
            Cif: excellentCif,
            BorrowerName: "Apex Manufacturing Ltd",
            IndustrySector: "Export Garments",
            FinancialStatementDate: new DateOnly(2025, 12, 31),
            AssessmentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            LeverageScore: 14m,
            LiquidityScore: 13m,
            ProfitabilityScore: 14m,
            CoverageScore: 14m,
            BusinessRiskScore: 13m,
            ManagementRiskScore: 14m,
            SecurityRiskScore: 9m,
            EvaluatedByUserId: "CREDIT-ANALYST-01"
        );

        var excRes = await api.PostAsync("/api/v1/crm/compliance/icrr/evaluate", new APIRequestContextOptions
        {
            DataObject = excellentCmd
        });
        excRes.Status.Should().Be(200);

        var excIcrr = (await excRes.JsonAsync())?.Deserialize<BorrowerIcrrAssessmentDto>(JsonOptions);
        excIcrr.Should().NotBeNull();
        excIcrr!.RatingGrade.Should().Be(IcrrGrade.Excellent);
        excIcrr.TotalIcrrScore.Should().Be(91m);

        // Check sanction clearance for Excellent
        var verifyExcRes = await api.GetAsync($"/api/v1/crm/compliance/icrr/{excellentCif}/verify-sanction");
        verifyExcRes.Status.Should().Be(200);

        // 2. Evaluate Unacceptable Borrower (Total = 38 points)
        var unaccCmd = new EvaluateBorrowerIcrrCommand(
            Cif: unaccCif,
            BorrowerName: "Distressed Commercial Ventures Ltd",
            IndustrySector: "Retail Trading",
            FinancialStatementDate: new DateOnly(2025, 12, 31),
            AssessmentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            LeverageScore: 5m,
            LiquidityScore: 5m,
            ProfitabilityScore: 4m,
            CoverageScore: 6m,
            BusinessRiskScore: 7m,
            ManagementRiskScore: 6m,
            SecurityRiskScore: 5m,
            EvaluatedByUserId: "CREDIT-ANALYST-01"
        );

        var unaccRes = await api.PostAsync("/api/v1/crm/compliance/icrr/evaluate", new APIRequestContextOptions
        {
            DataObject = unaccCmd
        });
        unaccRes.Status.Should().Be(200);

        var unaccIcrr = (await unaccRes.JsonAsync())?.Deserialize<BorrowerIcrrAssessmentDto>(JsonOptions);
        unaccIcrr.Should().NotBeNull();
        unaccIcrr!.RatingGrade.Should().Be(IcrrGrade.Unacceptable);

        // Verify sanction check strictly blocks Unacceptable borrower (400 Bad Request)
        var verifyUnaccRes = await api.GetAsync($"/api/v1/crm/compliance/icrr/{unaccCif}/verify-sanction");
        verifyUnaccRes.Status.Should().Be(400);

        // 3. Board of Directors Approves Exception with Risk Mitigation Strategy
        var boardApprovalCmd = new ApproveIcrrByBoardCommand(
            AssessmentId: unaccIcrr.Id,
            BoardResolutionReference: "BOD-RES-2026-11-8842",
            BoardResolutionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            BoardMitigationStrategy: "Mandated 120% cash collateral lien and hypothecation over prime property",
            ApprovedByUserId: "BOARD-SECRETARY-01"
        );

        var boardRes = await api.PostAsync("/api/v1/crm/compliance/icrr/approve-board", new APIRequestContextOptions
        {
            DataObject = boardApprovalCmd
        });
        boardRes.Status.Should().Be(200);

        var approvedIcrr = (await boardRes.JsonAsync())?.Deserialize<BorrowerIcrrAssessmentDto>(JsonOptions);
        approvedIcrr.Should().NotBeNull();
        approvedIcrr!.RatingGrade.Should().Be(IcrrGrade.Unacceptable);
        approvedIcrr.IsBoardApproved.Should().BeTrue();
        approvedIcrr.BoardResolutionReference.Should().Be("BOD-RES-2026-11-8842");

        // 4. Verify sanction check now permits facility due to Board Resolution
        var verifyAfterBoardRes = await api.GetAsync($"/api/v1/crm/compliance/icrr/{unaccCif}/verify-sanction");
        verifyAfterBoardRes.Status.Should().Be(200);
    }
}
