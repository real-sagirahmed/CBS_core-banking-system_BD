using System.Text.Json;
using CBS.Application.Customers.DTOs;
using CBS.Application.TradeFinance.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class TradeFinancePlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public TradeFinancePlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<string> CreateVerifiedCustomerAsync(IAPIRequestContext api, string fullName)
    {
        string uniqueNid = $"7{Random.Shared.Next(100000000, 999999999)}";
        string uniqueTin = $"8{Random.Shared.Next(10000000, 99999999)}333";

        var draftReq = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: fullName,
            DateOfBirth: new DateOnly(1982, 4, 15),
            Gender: "Male",
            FatherName: "Trade Father",
            MotherName: "Trade Mother",
            StreetAddress: "10 Agrabad C/A",
            ThanaOrUpazila: "Double Mooring",
            District: "Chittagong",
            PostalCode: "4100",
            MobileNumber: "+8801711998877",
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
    public async Task PlaywrightE2E_ImportLetterOfCredit_Draft_Authorize_GeneratesSwiftMt700_AndEnforcesDualControl()
    {
        // Arrange
        var api = await _fixture.CreateApiContextAsync();

        var cifNumber = await CreateVerifiedCustomerAsync(api, "Pacific Trade International");
        var applicantId = Guid.NewGuid();
        var lcNumber = $"LC-2026-001-{Random.Shared.Next(1000, 9999)}";

        var draftReq = new DraftDocumentaryCreditRequest(
            LcNumber: lcNumber,
            Type: LcType.ImportSight,
            ApplicantId: applicantId,
            BeneficiaryName: "Tokyo Steel Corporation",
            BeneficiaryAddress: "Chiyoda-ku, Tokyo, Japan",
            BeneficiaryCountry: "JAPAN",
            AdvisingBankBic: "BOTKJPJT",
            AdvisingBankName: "MUFG Bank, Tokyo",
            Amount: 350000m,
            Currency: "USD",
            CashMarginAmount: 70000m,
            TolerancePercentage: 5m,
            IssueDate: new DateTime(2026, 6, 1),
            ExpiryDate: new DateTime(2026, 9, 1),
            ExpiryPlace: "BANGLADESH",
            LatestShipmentDate: new DateTime(2026, 8, 15),
            PortOfLoading: "YOKOHAMA",
            PortOfDischarge: "CHATTOGRAM",
            GoodsDescription: "Hot Rolled Steel Coils",
            HsCode: "7207.11.00",
            Incoterm: Incoterm.CFR,
            IrcNumber: "IRC-DK-112233",
            IsIrcValid: true,
            IsCibDefaulter: false,
            MakerUserId: "MAKER_TRADE_01",
            DeclaredUnitPrice: 850m,
            BenchmarkMinPrice: 800m,
            BenchmarkMaxPrice: 900m);

        // Act 1: Draft LC
        var draftRes = await api.PostAsync("/api/v1/trade/lc/draft", new APIRequestContextOptions
        {
            DataObject = draftReq
        });
        draftRes.Status.Should().Be(201);
        var draftedLc = (await draftRes.JsonAsync())?.Deserialize<DocumentaryCreditResponse>(JsonOptions);
        draftedLc.Should().NotBeNull();
        draftedLc!.Status.Should().Be("Draft");
        draftedLc.RawSwiftMt700.Should().BeNull();

        // Act 2: Maker attempting to authorize own LC (Dual Control Violation BRPD 1.3.18, 10.3.5-x)
        var selfAuthRes = await api.PostAsync("/api/v1/trade/lc/authorize", new APIRequestContextOptions
        {
            DataObject = new AuthorizeDocumentaryCreditRequest(lcNumber, "MAKER_TRADE_01")
        });
        selfAuthRes.Status.Should().Be(400);
        var selfAuthError = await selfAuthRes.TextAsync();
        selfAuthError.Should().Contain("Dual-control separation of duty enforced");

        // Act 3: Independent Checker authorizes LC
        var checkerAuthRes = await api.PostAsync("/api/v1/trade/lc/authorize", new APIRequestContextOptions
        {
            DataObject = new AuthorizeDocumentaryCreditRequest(lcNumber, "CHECKER_TRADE_02", "S7BDBD2X")
        });
        checkerAuthRes.Status.Should().Be(200);
        var issuedLc = (await checkerAuthRes.JsonAsync())?.Deserialize<DocumentaryCreditResponse>(JsonOptions);
        issuedLc.Should().NotBeNull();
        issuedLc!.Status.Should().Be("Issued");
        issuedLc.CheckerUserId.Should().Be("CHECKER_TRADE_02");

        // Assert SWIFT MT700 Generation
        issuedLc.RawSwiftMt700.Should().NotBeNull();
        issuedLc.RawSwiftMt700.Should().Contain(":40A:IRREVOCABLE");
        issuedLc.RawSwiftMt700.Should().Contain($":20:{lcNumber}");
        issuedLc.RawSwiftMt700.Should().Contain(":32B:USD350000.00");
        issuedLc.RawSwiftMt700.Should().Contain("PRE-SHIPMENT INSPECTION (PSI)");

        // Act 4: Query by LC Number
        var getRes = await api.GetAsync($"/api/v1/trade/lc/{lcNumber}");
        getRes.Status.Should().Be(200);
        var fetchedLc = (await getRes.JsonAsync())?.Deserialize<DocumentaryCreditResponse>(JsonOptions);
        fetchedLc!.Status.Should().Be("Issued");
    }

    [Fact]
    public async Task PlaywrightE2E_ShippingGuarantee_Requires100PercentMargin_AndReleasesOnlyOnOriginalBlSurrender()
    {
        // Arrange
        var api = await _fixture.CreateApiContextAsync();

        var sgRef = $"SG-2026-001-{Random.Shared.Next(1000, 9999)}";
        var lcNum = $"LC-2026-IMP-7788";

        // Act 1: Attempt to issue SG with less than 100% margin (e.g. 50,000 USD margin on 100,000 USD guarantee)
        var invalidSgReq = new IssueShippingGuaranteeRequest(
            SgReference: sgRef,
            LcNumber: lcNum,
            ApplicantId: Guid.NewGuid(),
            CarrierName: "Mediterranean Shipping Company (MSC)",
            VesselName: "MSC Oscar",
            PortOfDischarge: "CHATTOGRAM",
            GuaranteeAmount: 100000m,
            Currency: "USD",
            CashMarginAmount: 50000m, // Only 50%
            MakerUserId: "OFFICER_01");

        var invalidRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/issue", new APIRequestContextOptions
        {
            DataObject = invalidSgReq
        });
        invalidRes.Status.Should().Be(400);
        var errorText = await invalidRes.TextAsync();
        errorText.Should().Contain("Shipping Guarantee strictly requires 100% cash margin");

        // Act 2: Issue SG with 100% cash margin (100,000 USD)
        var validSgReq = invalidSgReq with { CashMarginAmount = 100000m };
        var validRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/issue", new APIRequestContextOptions
        {
            DataObject = validSgReq
        });
        validRes.Status.Should().Be(201);
        var sg = (await validRes.JsonAsync())?.Deserialize<ShippingGuaranteeResponse>(JsonOptions);
        sg.Should().NotBeNull();
        sg!.Status.Should().Be("Active");

        // Act 3: Authorize SG
        var authRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/authorize", new APIRequestContextOptions
        {
            DataObject = new AuthorizeShippingGuaranteeRequest(sgRef, "SUPERVISOR_02")
        });
        authRes.Status.Should().Be(200);

        // Act 4: Attempt to release SG without original Bill of Lading (BRPD 5.1.2-vii)
        var emptyBlRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/release-bl", new APIRequestContextOptions
        {
            DataObject = new ReleaseShippingGuaranteeRequest(sgRef, string.Empty, "OFFICER_01")
        });
        emptyBlRes.Status.Should().Be(400);

        // Act 5: Release SG upon surrender of original Bill of Lading
        var releaseRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/release-bl", new APIRequestContextOptions
        {
            DataObject = new ReleaseShippingGuaranteeRequest(sgRef, "MSC-BL-2026-9900", "OFFICER_01")
        });
        releaseRes.Status.Should().Be(200);
        var releasedSg = (await releaseRes.JsonAsync())?.Deserialize<ShippingGuaranteeResponse>(JsonOptions);
        releasedSg.Should().NotBeNull();
        releasedSg!.Status.Should().Be("ReleasedUponBlSurrender");
        releasedSg.SurrenderedOriginalBlNumber.Should().Be("MSC-BL-2026-9900");
    }

    [Fact]
    public async Task PlaywrightE2E_ExportExpTracking_AndTbmlScreening_EnforcesStatutoryDeadlines_AndPriceVarianceAlerts()
    {
        // Arrange
        var api = await _fixture.CreateApiContextAsync();

        var expNumber = $"00112026{Random.Shared.Next(100000, 999999)}";
        var shipmentDate = new DateTime(2026, 1, 15);

        // Act 1: Register EXP Tracking
        var expReq = new RegisterExpRequest(
            ExpNumber: expNumber,
            ExportLcOrContractNumber: "MLC-2026-RMG-001",
            ExporterCustomerId: Guid.NewGuid(),
            DestinationCountry: "GERMANY",
            InvoiceAmount: 120000m,
            Currency: "EUR",
            ShipmentDate: shipmentDate,
            MakerUserId: "OFFICER_EXP");

        var registerRes = await api.PostAsync("/api/v1/trade/exp/register", new APIRequestContextOptions
        {
            DataObject = expReq
        });
        registerRes.Status.Should().Be(201);
        var exp = (await registerRes.JsonAsync())?.Deserialize<ExpRegulatoryResponse>(JsonOptions);
        exp.Should().NotBeNull();
        exp!.Status.Should().Be("InTransit");
        exp.RealizationDeadlineDate.Should().Be(shipmentDate.AddDays(120));

        // Act 2: Partial realization (50,000 EUR realized)
        var realizeRes = await api.PostAsync("/api/v1/trade/exp/realize", new APIRequestContextOptions
        {
            DataObject = new RealizeExpRequest(expNumber, 50000m, "EUR")
        });
        realizeRes.Status.Should().Be(200);
        var realizedExp = (await realizeRes.JsonAsync())?.Deserialize<ExpRegulatoryResponse>(JsonOptions);
        realizedExp!.RealizedAmount.Should().Be(50000m);
        realizedExp.Status.Should().Be("InTransit");

        // Act 3: Evaluate overdue status after statutory 120 days (e.g. 130 days later)
        var evaluateRes = await api.PostAsync("/api/v1/trade/exp/evaluate-overdue", new APIRequestContextOptions
        {
            DataObject = new EvaluateExpOverdueRequest(expNumber, shipmentDate.AddDays(130))
        });
        evaluateRes.Status.Should().Be(200);
        var overdueExp = (await evaluateRes.JsonAsync())?.Deserialize<ExpRegulatoryResponse>(JsonOptions);
        overdueExp!.Status.Should().Be("OverdueReportedToBangladeshBank");
        overdueExp.ReportedToBangladeshBank.Should().BeTrue();

        // Act 4: TBML Price Anomaly and Sanctioned Port Screening Test
        var tbmlReq = new TbmlScreeningRequest(
            EntitySanctionHit: false,
            DeclaredUnitPrice: 250m, // Benchmark max is 100m -> +150% variance (Over-invoicing!)
            BenchmarkMinPrice: 80m,
            BenchmarkMaxPrice: 100m,
            PortOfLoading: "BANDAR ABBAS", // High-risk / Sanctioned port!
            PortOfDischarge: "CHATTOGRAM",
            IsDualUseHsCode: false);

        var tbmlRes = await api.PostAsync("/api/v1/trade/tbml/screen", new APIRequestContextOptions
        {
            DataObject = tbmlReq
        });
        tbmlRes.Status.Should().Be(200);
        var tbml = (await tbmlRes.JsonAsync())?.Deserialize<TbmlScreeningResponse>(JsonOptions);
        tbml.Should().NotBeNull();
        tbml!.HasPriceVarianceAnomaly.Should().BeTrue();
        tbml.HasHighRiskPortRoute.Should().BeTrue();
        tbml.UnitPriceVariancePercentage.Should().Be(150m);
        tbml.RiskRating.Should().Be("High");
        tbml.RiskSummary.Should().Contain("Over-invoicing suspected");
        tbml.RiskSummary.Should().Contain("High-risk or sanctioned port");
    }
}
