// ARCHITECTURAL INTENT: Playwright API Integration tests locking Loan Asset Classification (CL),
// Qualitative Judgment (QJ) overrides, Rescheduling & Restructuring lifecycle, and Statutory Write-Off
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 & 4.2, BRPD 14/2012, 16/2020, 04/2023, 01/2013, 08/2023.

using System.Text.Json;
using CBS.Application.Customers.DTOs;
using CBS.Application.Lending.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class LoanClassificationAndRecoveryPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public LoanClassificationAndRecoveryPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<string> CreateVerifiedCustomerAsync(IAPIRequestContext api, string fullName)
    {
        string uniqueNid = $"7{Random.Shared.Next(100000000, 999999999)}";
        string uniqueTin = $"6{Random.Shared.Next(10000000, 99999999)}333";

        var draftReq = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: fullName,
            DateOfBirth: new DateOnly(1982, 5, 20),
            Gender: "Male",
            FatherName: "Father",
            MotherName: "Mother",
            StreetAddress: "77 Motijheel C/A",
            ThanaOrUpazila: "Motijheel",
            District: "Dhaka",
            PostalCode: "1000",
            MobileNumber: "+8801711223344",
            SectorCode: "9901",
            NationalId: uniqueNid,
            TaxId: uniqueTin);

        var draftRes = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = draftReq
        });
        draftRes.Status.Should().Be(201);
        var customer = (await draftRes.JsonAsync())?.Deserialize<CustomerResponseDto>(JsonOptions)!;

        var verifyRes = await api.PostAsync($"/api/v1/customers/{customer.CifNumber}/verify", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "CHECKER01" } },
            DataObject = new { isApproved = true }
        });
        verifyRes.Status.Should().Be(200);

        return customer.CifNumber;
    }

    private async Task<string> CreateAndDisburseLoanAsync(IAPIRequestContext api, string cif, decimal amount, string branchCode = "0101")
    {
        var createLoanReq = new CreateLoanAccountRequest(
            CifNumber: cif,
            FacilityType: LoanFacilityType.TermLoan,
            BranchCode: branchCode,
            SanctionLimit: amount,
            DrawingPower: amount,
            InterestRate: 9.00m,
            MaturityDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3)));

        var createRes = await api.PostAsync("/api/v1/lending/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Approver-Id", "SANCTION-MGR" } },
            DataObject = createLoanReq
        });
        createRes.Status.Should().Be(201);
        var loan = (await createRes.JsonAsync())?.Deserialize<LoanAccountResponseDto>(JsonOptions)!;

        // Vet CAD
        var vetRes = await api.PostAsync("/api/v1/lending/accounts/vet-cad", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Cad-Officer-Id", "CAD-OFFICER-01" } },
            DataObject = new VetCadChecklistRequest(loan.AccountNumber)
        });
        vetRes.Status.Should().Be(200);

        // Disburse
        var disburseRes = await api.PostAsync("/api/v1/lending/accounts/disburse", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Disbursing-Officer-Id", "DISBURSE-OFFICER-01" } },
            DataObject = new DisburseLoanRequest(loan.AccountNumber, amount)
        });
        disburseRes.Status.Should().Be(200);

        return loan.AccountNumber;
    }

    [Fact]
    public async Task PlaywrightE2E_LoanClassification_PortfolioEvaluationAndReporting()
    {
        var api = await _fixture.CreateApiContextAsync();
        string cif = await CreateVerifiedCustomerAsync(api, "Haji Mohammad Mohsin");
        string loanAccount = await CreateAndDisburseLoanAsync(api, cif, 1_000_000m, "0101");

        // 1. Evaluate Portfolio Classification for Branch 0101
        var evalRes = await api.PostAsync("/api/v1/lending/classification/evaluate-eod", new APIRequestContextOptions
        {
            DataObject = new { BranchCode = "0101", EvaluationDate = DateTime.UtcNow }
        });
        evalRes.Status.Should().Be(200);
        var batchResult = (await evalRes.JsonAsync())?.Deserialize<LoanClassificationBatchResultDto>(JsonOptions);
        batchResult.Should().NotBeNull();
        batchResult!.TotalAccountsEvaluated.Should().BeGreaterThan(0);
        batchResult.EvaluatedAccounts.Should().Contain(a => a.LoanAccountNumber == loanAccount && a.NewGrade == LoanClassificationGrade.Standard);

        // 2. Fetch Portfolio Classification Report
        var repRes = await api.GetAsync("/api/v1/lending/classification/portfolio-report?branchCode=0101");
        repRes.Status.Should().Be(200);
        var report = (await repRes.JsonAsync())?.Deserialize<LoanPortfolioClassificationReportDto>(JsonOptions);
        report.Should().NotBeNull();
        report!.TotalAccounts.Should().BeGreaterThan(0);
        report.GradeSummaries.Should().Contain(s => s.Grade == LoanClassificationGrade.Standard && s.AccountCount > 0);
    }

    [Fact]
    public async Task PlaywrightE2E_QualitativeJudgment_ApplyAndRevoke_EnforcesDowngradeAndDualControl()
    {
        var api = await _fixture.CreateApiContextAsync();
        string cif = await CreateVerifiedCustomerAsync(api, "Anwar Hossain Manju");
        string loanAccount = await CreateAndDisburseLoanAsync(api, cif, 800_000m, "0101");

        // 1. Same Inspector and Checker must be rejected (Dual-Control separation)
        var sameUserPayload = new
        {
            Grade = LoanClassificationGrade.SubStandard_SS,
            Reason = QualitativeJudgmentReason.CentralBankInspectionFinding,
            JustificationNotes = "Adverse audit remark",
            InspectionReportReference = "BB-INSP-2026-09",
            InspectorUserId = "INSPECTOR-SAME",
            CheckerUserId = "INSPECTOR-SAME"
        };
        var failDualRes = await api.PostAsync($"/api/v1/lending/loans/{loanAccount}/qualitative-judgment", new APIRequestContextOptions
        {
            DataObject = sameUserPayload
        });
        failDualRes.Status.Should().Be(400);
        (await failDualRes.TextAsync()).Should().Contain("MakerCheckerSeparationInvariant");

        // 2. Upgrade attempt via QJ must be rejected (Downgrade only)
        var upgradePayload = new
        {
            Grade = LoanClassificationGrade.Standard,
            Reason = QualitativeJudgmentReason.CentralBankInspectionFinding,
            JustificationNotes = "Attempting upgrade",
            InspectorUserId = "INSP-01",
            CheckerUserId = "CHK-01"
        };
        var failUpgradeRes = await api.PostAsync($"/api/v1/lending/loans/{loanAccount}/qualitative-judgment", new APIRequestContextOptions
        {
            DataObject = upgradePayload
        });
        failUpgradeRes.Status.Should().Be(400);
        (await failUpgradeRes.TextAsync()).Should().Contain("QualitativeJudgmentDowngradeOnlyInvariant");

        // 3. Valid Adverse QJ Downgrade (Standard -> SubStandard_SS)
        var validQjPayload = new
        {
            Grade = LoanClassificationGrade.SubStandard_SS,
            Reason = QualitativeJudgmentReason.BusinessSuspensionOrClosure,
            JustificationNotes = "Operations halted by regulatory order",
            InspectionReportReference = "INSP-DHAKA-2026-44",
            InspectorUserId = "INSP-01",
            CheckerUserId = "CHK-01"
        };
        var validQjRes = await api.PostAsync($"/api/v1/lending/loans/{loanAccount}/qualitative-judgment", new APIRequestContextOptions
        {
            DataObject = validQjPayload
        });
        validQjRes.Status.Should().Be(200);
        var qjResult = (await validQjRes.JsonAsync())?.Deserialize<QualitativeJudgmentResultDto>(JsonOptions);
        qjResult.Should().NotBeNull();
        qjResult!.AppliedGrade.Should().Be(LoanClassificationGrade.SubStandard_SS);
        qjResult.IsActive.Should().BeTrue();

        // 4. Revocation of QJ with Dual-Control
        var revokePayload = new
        {
            InspectorUserId = "INSP-01",
            CheckerUserId = "CHK-02",
            RevocationReason = "Operations resumed with formal license clearance"
        };
        var revokeRes = await api.PostAsync($"/api/v1/lending/loans/{loanAccount}/qualitative-judgment/revoke", new APIRequestContextOptions
        {
            DataObject = revokePayload
        });
        revokeRes.Status.Should().Be(200);
        var revokedResult = (await revokeRes.JsonAsync())?.Deserialize<QualitativeJudgmentResultDto>(JsonOptions);
        revokedResult.Should().NotBeNull();
        revokedResult!.IsActive.Should().BeFalse();
        revokedResult.AppliedGrade.Should().Be(LoanClassificationGrade.Standard);
    }

    [Fact]
    public async Task PlaywrightE2E_LoanRescheduling_MandatoryDownPaymentAndIterationLimits()
    {
        var api = await _fixture.CreateApiContextAsync();
        string cif = await CreateVerifiedCustomerAsync(api, "Sikder Enterprise");
        string loanAccount = await CreateAndDisburseLoanAsync(api, cif, 1_200_000m, "0101");

        // 1. Reschedule with insufficient down payment must fail
        var badDownPaymentPayload = new
        {
            RescheduleIteration = 1,
            DownPaymentPaid = 50_000m,
            RequiredDownPayment = 120_000m,
            NewMaturityDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4)),
            NewInterestRate = 9.50m,
            ApprovalAuthority = RescheduleApprovalAuthority.ManagingDirector,
            BoardResolutionReference = "",
            BoardApprovalDate = (DateOnly?)null,
            InterestWaiver = (decimal?)null,
            RestructuringNotes = "Payment insufficient",
            MakerUserId = "MAKER-01",
            CheckerUserId = "CHECKER-01"
        };
        var badRes = await api.PostAsync($"/api/v1/lending/loans/{loanAccount}/reschedule", new APIRequestContextOptions
        {
            DataObject = badDownPaymentPayload
        });
        badRes.Status.Should().Be(400);
        (await badRes.TextAsync()).Should().Contain("RescheduleDownPaymentInsufficientInvariant");

        // 2. Iteration 2 without board resolution must fail
        var missingBoardPayload = new
        {
            RescheduleIteration = 2,
            DownPaymentPaid = 150_000m,
            RequiredDownPayment = 150_000m,
            NewMaturityDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4)),
            NewInterestRate = 9.50m,
            ApprovalAuthority = RescheduleApprovalAuthority.BoardOfDirectors,
            BoardResolutionReference = "",
            BoardApprovalDate = (DateOnly?)null,
            InterestWaiver = (decimal?)null,
            RestructuringNotes = "Missing board approval ref",
            MakerUserId = "MAKER-01",
            CheckerUserId = "CHECKER-01"
        };
        var missingBoardRes = await api.PostAsync($"/api/v1/lending/loans/{loanAccount}/reschedule", new APIRequestContextOptions
        {
            DataObject = missingBoardPayload
        });
        missingBoardRes.Status.Should().Be(400);
        (await missingBoardRes.TextAsync()).Should().Contain("RescheduleBoardApprovalMandatoryInvariant");

        // 3. Valid Reschedule Iteration 1
        var validReschedulePayload = new
        {
            RescheduleIteration = 1,
            DownPaymentPaid = 150_000m,
            RequiredDownPayment = 150_000m,
            NewMaturityDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4)),
            NewInterestRate = 9.25m,
            ApprovalAuthority = RescheduleApprovalAuthority.ManagingDirector,
            BoardResolutionReference = "MD-APPROVAL-2026-01",
            BoardApprovalDate = DateOnly.FromDateTime(DateTime.UtcNow),
            InterestWaiver = (decimal?)null,
            RestructuringNotes = "Approved by MD per circular 16/2020",
            MakerUserId = "MAKER-01",
            CheckerUserId = "CHECKER-01"
        };
        var validRes = await api.PostAsync($"/api/v1/lending/loans/{loanAccount}/reschedule", new APIRequestContextOptions
        {
            DataObject = validReschedulePayload
        });
        validRes.Status.Should().Be(200);
        var resDto = (await validRes.JsonAsync())?.Deserialize<RescheduleResultDto>(JsonOptions);
        resDto.Should().NotBeNull();
        resDto!.RescheduleIteration.Should().Be(1);
        resDto.RescheduledPrincipal.Should().Be(1_050_000m); // 1.2M - 150k
        resDto.NewInterestRate.Should().Be(9.25m);
        resDto.Status.Should().Be(nameof(RescheduleStatus.Active));
    }

    [Fact]
    public async Task PlaywrightE2E_LoanWriteOff_PreconditionsAndPostWriteOffRecoveryWaterfall()
    {
        var api = await _fixture.CreateApiContextAsync();
        string cif = await CreateVerifiedCustomerAsync(api, "Bad Debt Mills Ltd");
        string loanAccount = await CreateAndDisburseLoanAsync(api, cif, 750_000m, "0101");

        // 1. Attempt write-off on Standard loan -> Rejected
        var prematureWriteOff = new
        {
            SpecificProvisionAvailable = 750_000m,
            BoardResolutionNumber = "BOD-WO-2026-05",
            BoardApprovalDate = DateOnly.FromDateTime(DateTime.UtcNow),
            LegalCaseType = LegalCaseType.ArthaRinAdalatAin2003,
            ArthaRinCaseNumber = "ARA-991/2026",
            ArthaRinFilingDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MakerUserId = "MAKER-01",
            CheckerUserId = "CHECKER-01",
            JustificationNotes = "Premature writeoff test"
        };
        var prematureRes = await api.PostAsync($"/api/v1/lending/loans/{loanAccount}/write-off", new APIRequestContextOptions
        {
            DataObject = prematureWriteOff
        });
        prematureRes.Status.Should().Be(400);
        (await prematureRes.TextAsync()).Should().Contain("WriteOffNotBadLossInvariant");

        // 2. Downgrade to BadLoss_BL via QJ
        var qjBlPayload = new
        {
            Grade = LoanClassificationGrade.BadLoss_BL,
            Reason = QualitativeJudgmentReason.FraudOrForgery,
            JustificationNotes = "Willful default and fraudulent collateral",
            InspectorUserId = "AUDIT-01",
            CheckerUserId = "AUDIT-02"
        };
        var qjRes = await api.PostAsync($"/api/v1/lending/loans/{loanAccount}/qualitative-judgment", new APIRequestContextOptions
        {
            DataObject = qjBlPayload
        });
        qjRes.Status.Should().Be(200);

        // 3. Attempt write-off without Artha Rin suit for facility > 500k -> Rejected
        var noArthaRinPayload = new
        {
            SpecificProvisionAvailable = 750_000m,
            BoardResolutionNumber = "BOD-WO-2026-05",
            BoardApprovalDate = DateOnly.FromDateTime(DateTime.UtcNow),
            LegalCaseType = LegalCaseType.ArthaRinAdalatAin2003,
            ArthaRinCaseNumber = "",
            ArthaRinFilingDate = (DateOnly?)null,
            MakerUserId = "MAKER-01",
            CheckerUserId = "CHECKER-01",
            JustificationNotes = "No lawsuit"
        };
        var noArthaRes = await api.PostAsync($"/api/v1/lending/loans/{loanAccount}/write-off", new APIRequestContextOptions
        {
            DataObject = noArthaRinPayload
        });
        noArthaRes.Status.Should().Be(400);
        (await noArthaRes.TextAsync()).Should().Contain("ArthaRinSuitFilingMandatoryInvariant");

        // 4. Valid Write-Off with 100% Provision & Artha Rin suit
        var validWriteOffPayload = new
        {
            SpecificProvisionAvailable = 750_000m,
            BoardResolutionNumber = "BOD-WO-2026-05",
            BoardApprovalDate = DateOnly.FromDateTime(DateTime.UtcNow),
            LegalCaseType = LegalCaseType.ArthaRinAdalatAin2003,
            ArthaRinCaseNumber = "ARA-991/2026",
            ArthaRinFilingDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MakerUserId = "MAKER-01",
            CheckerUserId = "CHECKER-01",
            JustificationNotes = "Board approval and court filing complete"
        };
        var validWoRes = await api.PostAsync($"/api/v1/lending/loans/{loanAccount}/write-off", new APIRequestContextOptions
        {
            DataObject = validWriteOffPayload
        });
        validWoRes.Status.Should().Be(200);
        var woDto = (await validWoRes.JsonAsync())?.Deserialize<WriteOffResultDto>(JsonOptions);
        woDto.Should().NotBeNull();
        woDto!.WrittenOffPrincipal.Should().Be(750_000m);
        woDto.Status.Should().Be(nameof(WriteOffStatus.WrittenOff));

        // 5. Verify Written-Off Register
        var regRes = await api.GetAsync("/api/v1/lending/written-off-register");
        regRes.Status.Should().Be(200);
        var regList = (await regRes.JsonAsync())?.Deserialize<IReadOnlyList<WrittenOffRegisterItemDto>>(JsonOptions);
        regList.Should().NotBeNull();
        regList.Should().Contain(r => r.LoanAccountNumber == loanAccount && r.WrittenOffPrincipal == 750_000m);

        // 6. Record Post-Writeoff Cash Recovery
        var recoveryPayload = new
        {
            Amount = 300_000m,
            RecoverySource = "Borrower voluntary surrender of auction asset",
            ReferenceNumber = "REC-VOUCHER-2026-90",
            ReceivedByUserId = "RECOVERY-OFFICER-01"
        };
        var recRes = await api.PostAsync($"/api/v1/lending/loans/{loanAccount}/write-off-recovery", new APIRequestContextOptions
        {
            DataObject = recoveryPayload
        });
        recRes.Status.Should().Be(200);
        var recDto = (await recRes.JsonAsync())?.Deserialize<WriteOffRecoveryResultDto>(JsonOptions);
        recDto.Should().NotBeNull();
        recDto!.AmountRecovered.Should().Be(300_000m);
        recDto.AllocatedToPrincipal.Should().Be(300_000m);
        recDto.CumulativeRecoveredAmount.Should().Be(300_000m);
        recDto.Status.Should().Be(nameof(WriteOffStatus.PartiallyRecovered));
    }
}
