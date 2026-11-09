using System.Text.Json;
using CBS.Application.Customers.DTOs;
using CBS.Application.Lending.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class LendingPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public LendingPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<string> CreateVerifiedCustomerAsync(IAPIRequestContext api, string fullName)
    {
        string uniqueNid = $"6{Random.Shared.Next(100000000, 999999999)}";
        string uniqueTin = $"5{Random.Shared.Next(10000000, 99999999)}222";

        var draftReq = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: fullName,
            DateOfBirth: new DateOnly(1975, 12, 10),
            Gender: "Male",
            FatherName: "Father",
            MotherName: "Mother",
            StreetAddress: "15 Agrabad Commercial Area",
            ThanaOrUpazila: "Double Mooring",
            District: "Chittagong",
            PostalCode: "4100",
            MobileNumber: "+8801911223344",
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

    [Fact]
    public async Task PlaywrightE2E_Lending_CadVettingAndDutySegregation_EnforcedBeforeDisbursement()
    {
        var api = await _fixture.CreateApiContextAsync();
        string cif = await CreateVerifiedCustomerAsync(api, "Akhtaruzzaman Chowdhury");

        // 1. Sanction Term Loan facility (Limit 1,000,000 BDT, Drawing Power 800,000 BDT)
        var createLoanReq = new CreateLoanAccountRequest(
            CifNumber: cif,
            FacilityType: LoanFacilityType.TermLoan,
            BranchCode: "001",
            SanctionLimit: 1000000m,
            DrawingPower: 800000m,
            InterestRate: 9.00m,
            MaturityDate: new DateOnly(2029, 11, 1));

        var createRes = await api.PostAsync("/api/v1/lending/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Approver-Id", "CREDIT_COMMITTEE_HO" } },
            DataObject = createLoanReq
        });
        createRes.Status.Should().Be(201);
        var loan = (await createRes.JsonAsync())?.Deserialize<LoanAccountResponseDto>(JsonOptions)!;
        loan.IsCadVetted.Should().BeFalse();
        loan.OutstandingPrincipal.Should().Be(0m);

        // 2. Attempt to disburse 300,000 BDT BEFORE CAD checklist vetting -> Must fail (BRPD 4.1.3-i)
        var unvettedDisburseReq = new DisburseLoanRequest(loan.AccountNumber, 300000m);
        var unvettedRes = await api.PostAsync("/api/v1/lending/accounts/disburse", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Disbursing-Officer-Id", "BRANCH_OFFICER_01" } },
            DataObject = unvettedDisburseReq
        });
        unvettedRes.Status.Should().Be(400);
        var unvettedBody = await unvettedRes.TextAsync();
        unvettedBody.Should().Contain("CadChecklistNotVettedInvariant");

        // 3. Credit Administration Department (CAD) vets documentation and signs off
        var vetReq = new VetCadChecklistRequest(loan.AccountNumber);
        var vetRes = await api.PostAsync("/api/v1/lending/accounts/vet-cad", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Cad-Officer-Id", "CAD_OFFICER_HEAD_OFFICE" } },
            DataObject = vetReq
        });
        vetRes.Status.Should().Be(200);
        var vettedLoan = (await vetRes.JsonAsync())?.Deserialize<LoanAccountResponseDto>(JsonOptions)!;
        vettedLoan.IsCadVetted.Should().BeTrue();
        vettedLoan.CadVettedByUserId.Should().Be("CAD_OFFICER_HEAD_OFFICE");

        // 4. CAD vetting officer attempts to disburse funds -> Must fail (Maker-Checker / 4-eyes segregation)
        var sameOfficerRes = await api.PostAsync("/api/v1/lending/accounts/disburse", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Disbursing-Officer-Id", "CAD_OFFICER_HEAD_OFFICE" } },
            DataObject = unvettedDisburseReq
        });
        sameOfficerRes.Status.Should().Be(400);
        var sameOfficerBody = await sameOfficerRes.TextAsync();
        sameOfficerBody.Should().Contain("MakerCheckerSeparationInvariant");

        // 5. Branch officer attempts to disburse 900,000 BDT (exceeds 800k Drawing Power) -> Must fail
        var dpExceedReq = new DisburseLoanRequest(loan.AccountNumber, 900000m);
        var dpExceedRes = await api.PostAsync("/api/v1/lending/accounts/disburse", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Disbursing-Officer-Id", "BRANCH_DISBURSER_02" } },
            DataObject = dpExceedReq
        });
        dpExceedRes.Status.Should().Be(400);
        var dpExceedBody = await dpExceedRes.TextAsync();
        dpExceedBody.Should().Contain("DrawingPowerExceededInvariant");

        // 6. Branch officer disburses valid 500,000 BDT -> Succeeds
        var validDisburseReq = new DisburseLoanRequest(loan.AccountNumber, 500000m);
        var validRes = await api.PostAsync("/api/v1/lending/accounts/disburse", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Disbursing-Officer-Id", "BRANCH_DISBURSER_02" } },
            DataObject = validDisburseReq
        });
        validRes.Status.Should().Be(200);
        var disbursedLoan = (await validRes.JsonAsync())?.Deserialize<LoanAccountResponseDto>(JsonOptions)!;
        disbursedLoan.OutstandingPrincipal.Should().Be(500000m);
        disbursedLoan.DisbursedByUserId.Should().Be("BRANCH_DISBURSER_02");
    }

    [Fact]
    public async Task PlaywrightE2E_Islamic_BaiMurabaha_LocksDisbursementToDirectGoodsSupplier()
    {
        var api = await _fixture.CreateApiContextAsync();
        string cif = await CreateVerifiedCustomerAsync(api, "Al-Haj Shamsul Huda");

        // 1. Sanction Bai-Murabaha facility (600,000 BDT)
        var createLoanReq = new CreateLoanAccountRequest(
            CifNumber: cif,
            FacilityType: LoanFacilityType.Islamic_BaiMurabaha,
            BranchCode: "001",
            SanctionLimit: 600000m,
            DrawingPower: 600000m,
            InterestRate: 8.50m,
            MaturityDate: new DateOnly(2027, 11, 1));

        var createRes = await api.PostAsync("/api/v1/lending/accounts", new APIRequestContextOptions
        {
            DataObject = createLoanReq
        });
        createRes.Status.Should().Be(201);
        var loan = (await createRes.JsonAsync())?.Deserialize<LoanAccountResponseDto>(JsonOptions)!;
        loan.IsShariahCompliant.Should().BeTrue();

        // 2. CAD officer vets checklist
        await api.PostAsync("/api/v1/lending/accounts/vet-cad", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Cad-Officer-Id", "CAD_VETTER" } },
            DataObject = new VetCadChecklistRequest(loan.AccountNumber)
        });

        // 3. Attempting to disburse without verified supplier beneficiary -> Must fail (BRPD 4.1.5-iv)
        var cashDisburseReq = new DisburseLoanRequest(loan.AccountNumber, 400000m, SupplierBeneficiary: null);
        var cashRes = await api.PostAsync("/api/v1/lending/accounts/disburse", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Disbursing-Officer-Id", "BRANCH_TELLER_01" } },
            DataObject = cashDisburseReq
        });
        cashRes.Status.Should().Be(400);
        var cashBody = await cashRes.TextAsync();
        cashBody.Should().Contain("MurabahaDirectVendorDisbursementInvariant");

        // 4. Disbursing with verified goods supplier beneficiary -> Succeeds
        var vendorDisburseReq = new DisburseLoanRequest(loan.AccountNumber, 400000m, SupplierBeneficiary: "BSRM Steels Limited");
        var vendorRes = await api.PostAsync("/api/v1/lending/accounts/disburse", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Disbursing-Officer-Id", "BRANCH_TELLER_01" } },
            DataObject = vendorDisburseReq
        });
        vendorRes.Status.Should().Be(200);
        var vendorLoan = (await vendorRes.JsonAsync())?.Deserialize<LoanAccountResponseDto>(JsonOptions)!;
        vendorLoan.OutstandingPrincipal.Should().Be(400000m);
        vendorLoan.DisbursementBeneficiarySupplier.Should().Be("BSRM Steels Limited");
    }

    [Fact]
    public async Task PlaywrightE2E_Lending_AssetClassificationAndRepaymentWaterfall_Enforced()
    {
        var api = await _fixture.CreateApiContextAsync();
        string cif = await CreateVerifiedCustomerAsync(api, "Beximco SME Unit");

        // 1. Create and disburse loan of 300,000 BDT
        var createLoanReq = new CreateLoanAccountRequest(
            CifNumber: cif,
            FacilityType: LoanFacilityType.ContinuousLoan_OD,
            BranchCode: "001",
            SanctionLimit: 500000m,
            DrawingPower: 500000m,
            InterestRate: 10.00m,
            MaturityDate: new DateOnly(2027, 1, 1));

        var createRes = await api.PostAsync("/api/v1/lending/accounts", new APIRequestContextOptions
        {
            DataObject = createLoanReq
        });
        var loan = (await createRes.JsonAsync())?.Deserialize<LoanAccountResponseDto>(JsonOptions)!;

        await api.PostAsync("/api/v1/lending/accounts/vet-cad", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Cad-Officer-Id", "CAD_VET_01" } },
            DataObject = new VetCadChecklistRequest(loan.AccountNumber)
        });

        await api.PostAsync("/api/v1/lending/accounts/disburse", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-Disbursing-Officer-Id", "DISBURSER_01" } },
            DataObject = new DisburseLoanRequest(loan.AccountNumber, 300000m)
        });

        // 2. Evaluate asset classification with DPD 200 days -> SubStandard_SS (BRPD 4.1.11)
        var classRes = await api.PostAsync("/api/v1/lending/accounts/evaluate-classification", new APIRequestContextOptions
        {
            DataObject = new EvaluateLoanClassificationRequest(loan.AccountNumber, 200)
        });
        classRes.Status.Should().Be(200);
        var classifiedLoan = (await classRes.JsonAsync())?.Deserialize<LoanAccountResponseDto>(JsonOptions)!;
        classifiedLoan.Classification.Should().Be(LoanClassificationGrade.SubStandard_SS);
        classifiedLoan.DaysPastDue.Should().Be(200);

        // 3. Process Repayment of 50,000 BDT through statutory recovery waterfall
        var repayRes = await api.PostAsync("/api/v1/lending/accounts/repayment", new APIRequestContextOptions
        {
            DataObject = new ProcessLoanRepaymentRequest(loan.AccountNumber, 50000m)
        });
        repayRes.Status.Should().Be(200);
        var repayResult = (await repayRes.JsonAsync())?.Deserialize<RepaymentResultResponseDto>(JsonOptions)!;

        repayResult.TotalPaid.Should().Be(50000m);
        // With no charges or overdue interest, all 50,000 BDT reduces principal
        repayResult.AllocatedPrincipal.Should().Be(50000m);
        repayResult.RemainingOutstandingPrincipal.Should().Be(250000m); // 300k - 50k
    }
}
