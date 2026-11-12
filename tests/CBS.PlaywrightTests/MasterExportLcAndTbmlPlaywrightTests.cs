using System.Text.Json;
using CBS.Application.Customers.DTOs;
using CBS.Application.TradeFinance.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

// ARCHITECTURAL INTENT: End-to-End integration tests asserting Master Export LC, B2B Import LC,
// and TBML Prevention Engine conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 5.0, 5.5, 5.6 & 5.7.
public class MasterExportLcAndTbmlPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public MasterExportLcAndTbmlPlaywrightTests(CbsApiServerFixture fixture)
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
            DateOfBirth: new DateOnly(1985, 5, 20),
            Gender: "Male",
            FatherName: "Exporter Father",
            MotherName: "Exporter Mother",
            StreetAddress: "EPZ Export Zone",
            ThanaOrUpazila: "Double Mooring",
            District: "Chittagong",
            PostalCode: "4204",
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

    [Fact]
    public async Task Test_MasterExportLc_Lifecycle_Registration_Authorization_B2bIssuance_Amendment_Closure()
    {
        var api = await _fixture.CreateApiContextAsync();
        string exporterCif = await CreateVerifiedCustomerAsync(api, "Ananta Apparels Export Ltd");

        string masterLcNumber = $"MLC-2026-EXP-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiryDate = issueDate.AddMonths(6);
        var shipmentDate = issueDate.AddMonths(5);

        // 1. Register Master Export LC (BRPD 5.6.1)
        // Total: 1,000,000 USD, Value Addition: 25% -> Permissible B2B Ceiling = 750,000 USD
        var registerPayload = new RegisterMasterExportLcRequest(
            masterLcNumber,
            MasterContractType.ExportLetterOfCredit,
            exporterCif,
            "HSBCBDDH",
            "HSBC Bank plc London",
            "H&M Hennes & Mauritz AB",
            "SE",
            1_000_000m,
            "USD",
            25.00m,
            issueDate,
            expiryDate,
            shipmentDate,
            "0101",
            "MAKER01");

        var regRes = await api.PostAsync("/api/v1/trade/master-lcs/register", new() { DataObject = registerPayload });
        regRes.Status.Should().Be(201);

        var registered = JsonSerializer.Deserialize<MasterExportLcResponseDto>(await regRes.TextAsync(), JsonOptions);
        registered.Should().NotBeNull();
        registered!.MasterLcNumber.Should().Be(masterLcNumber);
        registered.Status.Should().Be(nameof(MasterLcStatus.Draft));
        registered.MaxPermissibleB2bAmount.Should().Be(750_000m);
        registered.RemainingB2bCapacity.Should().Be(750_000m);

        // 2. Dual-Control Authorization Check (BRPD 1.3.18, 10.3.5-x)
        // Self-authorization violation
        var selfAuthRes = await api.PostAsync($"/api/v1/trade/master-lcs/{masterLcNumber}/authorize", new()
        {
            DataObject = new AuthorizeMasterExportLcRequest("MAKER01")
        });
        selfAuthRes.Status.Should().Be(400);

        // Distinct checker authorizes successfully
        var authRes = await api.PostAsync($"/api/v1/trade/master-lcs/{masterLcNumber}/authorize", new()
        {
            DataObject = new AuthorizeMasterExportLcRequest("CHECKER01")
        });
        authRes.Status.Should().Be(200);

        var authorized = JsonSerializer.Deserialize<MasterExportLcResponseDto>(await authRes.TextAsync(), JsonOptions);
        authorized!.Status.Should().Be(nameof(MasterLcStatus.Active));
        authorized.CheckerUserId.Should().Be("CHECKER01");

        // 3. Issue Back-to-Back LC under Master LC (BRPD 5.5.1, 5.6.1)
        string b2bLcNumber = $"B2B-2026-IMP-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var b2bShipment = issueDate.AddMonths(3);
        var b2bExpiry = issueDate.AddMonths(4);

        var b2bPayload = new OpenB2bLcUnderMasterRequest(
            b2bLcNumber,
            400_000m,
            "USD",
            0m,
            b2bShipment,
            b2bExpiry,
            "Xinjiang Yihua Chemical Industry Co.",
            "CN",
            "BKCHCNBJ",
            "Bank of China Beijing",
            "PI-CN-2026-8801",
            issueDate,
            "LCAF-0101-2026-101",
            "IMP-BB-2026-901",
            "100% Cotton Yarn",
            "5205.12",
            "MAKER01",
            "0101");

        var b2bRes = await api.PostAsync($"/api/v1/trade/master-lcs/{masterLcNumber}/b2b-lcs", new() { DataObject = b2bPayload });
        b2bRes.Status.Should().Be(201);

        var b2bIssued = JsonSerializer.Deserialize<B2bLcIssuedResponseDto>(await b2bRes.TextAsync(), JsonOptions);
        b2bIssued.Should().NotBeNull();
        b2bIssued!.B2bLcNumber.Should().Be(b2bLcNumber);
        b2bIssued.Amount.Should().Be(400_000m);
        b2bIssued.RemainingMasterCapacity.Should().Be(350_000m);

        // 4. Test Statutory Value Addition Ceiling Breach (BRPD 5.6.1-ii)
        // Trying to issue 450,000 USD when remaining capacity is only 350,000 USD
        string b2bExcessNumber = $"B2B-EXCESS-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var excessPayload = b2bPayload with { B2bLcNumber = b2bExcessNumber, Amount = 450_000m };

        var excessRes = await api.PostAsync($"/api/v1/trade/master-lcs/{masterLcNumber}/b2b-lcs", new() { DataObject = excessPayload });
        excessRes.Status.Should().Be(400);

        // 5. Test Date Hierarchy Violation (BRPD 5.6.1-vi)
        // B2B shipment date >= Master LC shipment date
        string b2bDateViolatingNumber = $"B2B-DATE-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var dateViolatingPayload = b2bPayload with
        {
            B2bLcNumber = b2bDateViolatingNumber,
            Amount = 100_000m,
            ShipmentDate = shipmentDate.AddDays(1)
        };

        var dateViolateRes = await api.PostAsync($"/api/v1/trade/master-lcs/{masterLcNumber}/b2b-lcs", new() { DataObject = dateViolatingPayload });
        dateViolateRes.Status.Should().Be(400);

        // 6. Test Master LC Value Decrease Invariant (BRPD 5.6.1-iii)
        // Active B2B is 400,000 USD. If Master is decreased to 500,000 USD, new ceiling is 375,000 USD < 400,000 USD.
        var decreasePayload = new AmendMasterLcRequest(500_000m, "MAKER01", "CHECKER01");
        var decreaseRes = await api.PostAsync($"/api/v1/trade/master-lcs/{masterLcNumber}/amend", new() { DataObject = decreasePayload });
        decreaseRes.Status.Should().Be(400);

        // 7. Check Master LC Liability & History Report (BRPD 5.6.1-xi)
        var historyRes = await api.GetAsync($"/api/v1/trade/master-lcs/{masterLcNumber}/history");
        historyRes.Status.Should().Be(200);

        var history = JsonSerializer.Deserialize<MasterLcHistoryReportDto>(await historyRes.TextAsync(), JsonOptions);
        history.Should().NotBeNull();
        history!.CumulativeB2bAllocatedAmount.Should().Be(400_000m);
        history.RemainingCapacity.Should().Be(350_000m);

        // 8. Test Master LC Cannot Close With Active B2Bs (BRPD 5.6.1-viii)
        var closePayload = new CloseMasterLcRequest("MAKER01", "CHECKER01");
        var closeRes = await api.PostAsync($"/api/v1/trade/master-lcs/{masterLcNumber}/close", new() { DataObject = closePayload });
        closeRes.Status.Should().Be(400);
    }

    [Fact]
    public async Task Test_TbmlPrevention_Ttp_PriceVerification_Screening_CamlcoEscalation()
    {
        var api = await _fixture.CreateApiContextAsync();
        string customerCif = await CreateVerifiedCustomerAsync(api, "Pacific Cottons Ltd");

        // 1. Create Customer Trade Transaction Profile (TTP) (BRPD 5.7.1)
        var ttpPayload = new CreateOrUpdateTtpRequest(
            customerCif,
            ApprovedAnnualImportVolume: 2_000_000m,
            ApprovedAnnualExportVolume: 3_000_000m,
            Currency: "USD",
            PermittedCountriesCsv: "US,GB,DE,CN",
            PermittedHsPrefixesCsv: "5201,5205,6109",
            RiskCategory: "Medium",
            EffectiveFromDate: new DateOnly(2026, 1, 1),
            ExpiryDate: new DateOnly(2026, 12, 31),
            ApprovedByUserId: "COMPLIANCE_HEAD01");

        var ttpRes = await api.PostAsync("/api/v1/trade/tbml/ttp", new() { DataObject = ttpPayload });
        ttpRes.Status.Should().Be(200);

        var ttpGetRes = await api.GetAsync($"/api/v1/trade/tbml/ttp/{customerCif}");
        ttpGetRes.Status.Should().Be(200);
        var ttp = JsonSerializer.Deserialize<TradeTransactionProfileDto>(await ttpGetRes.TextAsync(), JsonOptions);
        ttp.Should().NotBeNull();
        ttp!.CustomerCif.Should().Be(customerCif);
        ttp.ApprovedAnnualImportVolume.Should().Be(2_000_000m);

        // 2. Uniform Template for Price Verification (BRPD 5.7.6)
        // Benchmark exists in seed for Cotton 5201.00 ($2.00 - $2.50 / KG)
        var verifyPricePayload = new VerifyPriceRequest(
            HsCode: "5201.00",
            DeclaredUnitPrice: 3.50m, // 40% higher than $2.50 ceiling -> Over-Invoicing
            Currency: "USD",
            UnitOfMeasure: "KG",
            IsExport: false);

        var priceRes = await api.PostAsync("/api/v1/trade/tbml/verify-price", new() { DataObject = verifyPricePayload });
        priceRes.Status.Should().Be(200);

        var priceReport = JsonSerializer.Deserialize<PriceVerificationReportDto>(await priceRes.TextAsync(), JsonOptions);
        priceReport.Should().NotBeNull();
        priceReport!.HasVarianceAnomaly.Should().BeTrue();
        priceReport.VariancePercentage.Should().Be(40.00m);
        priceReport.AnomalyType.Should().Be(nameof(TbmlAlertType.PriceVarianceOverInvoicing));

        // 3. Screen Trade Transaction for TBML Anomaly Detection (BRPD 5.7.7)
        // High-risk tax haven country (Cayman Islands / KY) & Land-locked port
        var screenPayload = new ScreenTbmlRequest(
            ReferenceNumber: "LC-IMP-2026-7001",
            CustomerCif: customerCif,
            Amount: 500_000m,
            Currency: "USD",
            DeclaredUnitPrice: 3.50m,
            HsCode: "5201.00",
            PortOfLoading: "GEORGE TOWN",
            PortOfDischarge: "CHITTAGONG PORT",
            CounterpartyCountry: "KY",
            VesselName: "PACIFIC VOYAGER",
            VesselImo: "9876543",
            IsExport: false);

        var screenRes = await api.PostAsync("/api/v1/trade/tbml/screen-transaction", new() { DataObject = screenPayload });
        var screenText = await screenRes.TextAsync();
        screenRes.Status.Should().Be(200, $"API returned error: {screenText}");

        var screenReport = JsonSerializer.Deserialize<TbmlScreeningReportDto>(await screenRes.TextAsync(), JsonOptions);
        screenReport.Should().NotBeNull();
        screenReport!.IsPassed.Should().BeFalse();
        screenReport.Anomalies.Should().NotBeEmpty();
        screenReport.GeneratedAlerts.Should().NotBeEmpty();

        var firstAlert = screenReport.GeneratedAlerts.First();

        // 4. Track Vessel Audit Trail (BRPD 5.7.5)
        var trackVesselPayload = new TrackVesselRequest(
            "PACIFIC VOYAGER",
            "9876543",
            "PA",
            "PORT OF SINGAPORE",
            "CHITTAGONG PORT",
            "LOGISTICS_OFFICER_01");

        var vesselRes = await api.PostAsync("/api/v1/trade/tbml/vessels/track", new() { DataObject = trackVesselPayload });
        vesselRes.Status.Should().Be(201);

        // 5. Escalate TBML Alert to CAMLCO for STR/SAR Filing (BRPD 5.7.8)
        var escalatePayload = new EscalateTbmlAlertRequest(
            OfficerUserId: "CAMLCO_OFFICER_01",
            StrSarReference: "BFIU-STR-2026-CHITTAGONG-001",
            Remarks: "Confirmed capital flight pattern and tax haven entity routing; escalated to BFIU.");

        var escalateRes = await api.PostAsync($"/api/v1/trade/tbml/alerts/{firstAlert.AlertId}/escalate-str", new() { DataObject = escalatePayload });
        escalateRes.Status.Should().Be(200);

        var escalatedAlert = JsonSerializer.Deserialize<TbmlAlertDto>(await escalateRes.TextAsync(), JsonOptions);
        escalatedAlert.Should().NotBeNull();
        escalatedAlert!.IsEscalatedToCamlco.Should().BeTrue();
        escalatedAlert.Status.Should().Be(nameof(TbmlAlertStatus.EscalatedToCamlco));
        escalatedAlert.StrSarReference.Should().Be("BFIU-STR-2026-CHITTAGONG-001");

        // 6. List Active Alerts
        var activeAlertsRes = await api.GetAsync("/api/v1/trade/tbml/alerts/active");
        activeAlertsRes.Status.Should().Be(200);
    }
}
