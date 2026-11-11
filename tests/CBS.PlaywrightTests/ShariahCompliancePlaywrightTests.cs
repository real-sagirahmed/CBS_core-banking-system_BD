using System.Text.Json;
using CBS.Application.Shariah.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class ShariahCompliancePlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ShariahCompliancePlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Test_MudarabaPool_Creation_WeightageConfig_AndProfitDistribution()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Create Mudaraba Investment Pool
        var createPayload = new CreateMudarabaPoolRequestDto(
            "Agrani Mudaraba Special Pool",
            MudarabaPoolType.SpecialInvestmentPool,
            "BDT",
            30.0m, // 30% Mudarib, 70% Rab-ul-Mal
            2.0m,  // 2% PER
            1.0m); // 1% IRR

        var createRes = await api.PostAsync("/api/v1/shariah/pools", new()
        {
            DataObject = createPayload
        });
        createRes.Status.Should().Be(201);

        var pool = JsonSerializer.Deserialize<MudarabaPoolDto>(await createRes.TextAsync(), JsonOptions);
        pool.Should().NotBeNull();
        pool!.MudaribShareRatio.Should().Be(30.0m);
        pool.RabUlMalShareRatio.Should().Be(70.0m);

        // 2. Configure Product Weightage
        var weightagePayload = new ConfigureWeightageRequestDto(
            "MSB-SPEC",
            "Special Mudaraba Savings",
            1.20m,
            6.25m);

        var weightageRes = await api.PostAsync($"/api/v1/shariah/pools/{pool.Id}/weightages", new()
        {
            DataObject = weightagePayload
        });
        weightageRes.Status.Should().Be(200);

        // 3. Execute Profit Distribution
        var distRes = await api.PostAsync($"/api/v1/shariah/pools/{pool.Id}/distribute", new()
        {
            DataObject = new DistributePoolProfitRequestDto(2000000m) // 2 Million gross yield
        });
        distRes.Status.Should().Be(200);

        var distResult = JsonSerializer.Deserialize<PoolProfitDistributionResultDto>(await distRes.TextAsync(), JsonOptions);
        distResult.Should().NotBeNull();
        // Gross: 2,000,000
        // Bank (30%): 600,000
        // Rab-ul-Mal (70%): 1,400,000
        // PER (2% of 1,400,000): 28,000
        // IRR (1% of 1,400,000): 14,000
        // Net Distributable: 1,400,000 - 28,000 - 14,000 = 1,358,000
        distResult!.MudaribShareAmount.Should().Be(600000m);
        distResult.RabUlMalGrossShare.Should().Be(1400000m);
        distResult.PerDeductionAmount.Should().Be(28000m);
        distResult.IrrDeductionAmount.Should().Be(14000m);
        distResult.NetDistributableProfit.Should().Be(1358000m);

        // 4. Query All Pools
        var listRes = await api.GetAsync("/api/v1/shariah/pools");
        listRes.Status.Should().Be(200);
        var pools = JsonSerializer.Deserialize<List<MudarabaPoolDto>>(await listRes.TextAsync(), JsonOptions);
        pools.Should().NotBeNull();
        pools!.Should().Contain(p => p.Id == pool.Id);
    }

    [Fact]
    public async Task Test_AlWadiah_AccountOpening_AndZeroInterestYield()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Open Al-Wadiah Current Account
        var openPayload = new OpenAlWadiahAccountRequestDto(
            "1018888777766",
            "CIF-ISLAMIC-001",
            "BDT",
            "SAFE-CUSTODY-CONTRACT-2026-88");

        var openRes = await api.PostAsync("/api/v1/shariah/al-wadiah/open", new()
        {
            DataObject = openPayload
        });
        openRes.Status.Should().Be(201);

        var account = JsonSerializer.Deserialize<AlWadiahAccountDto>(await openRes.TextAsync(), JsonOptions);
        account.Should().NotBeNull();
        account!.AccountNumber.Should().Be("1018888777766");
        account.InterestRate.Should().Be(0.00m);
        account.IsActive.Should().BeTrue();

        // 2. Fetch Al-Wadiah Account
        var getRes = await api.GetAsync("/api/v1/shariah/al-wadiah/1018888777766");
        getRes.Status.Should().Be(200);
        var fetched = JsonSerializer.Deserialize<AlWadiahAccountDto>(await getRes.TextAsync(), JsonOptions);
        fetched.Should().NotBeNull();
        fetched!.AccountNumber.Should().Be("1018888777766");
        fetched.InterestRate.Should().Be(0.00m);
    }

    [Fact]
    public async Task Test_Murabaha_VendorDisbursementLock_RejectsUninspectedAndDirectCredit()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Initiate Bai-Murabaha Facility
        var createPayload = new CreateMurabahaFacilityRequestDto(
            "MUR-E2E-001",
            "CIF-CORP-E2E",
            "0101",
            4000000m, // Purchase Cost
            480000m,  // Profit Margin
            24,
            "Industrial Steam Boiler Machinery");

        var createRes = await api.PostAsync("/api/v1/shariah/murabaha/create", new()
        {
            DataObject = createPayload
        });
        createRes.Status.Should().Be(201);

        var facility = JsonSerializer.Deserialize<MurabahaFacilityDto>(await createRes.TextAsync(), JsonOptions);
        facility.Should().NotBeNull();
        facility!.Status.Should().Be(ShariahFacilityStatus.Draft);

        // 2. Attempt Disbursement BEFORE physical inspection (Must fail under BRPD 4.1.5-iv)
        var prematureOrder = new DisburseMurabahaToVendorRequestDto(
            "Boiler Tech Engineering Ltd",
            "1012222333344",
            "085270115",
            "INV-BOILER-01",
            4000000m,
            "MGR-001");

        var failDisburseRes = await api.PostAsync($"/api/v1/shariah/murabaha/{facility.Id}/disburse-vendor", new()
        {
            DataObject = prematureOrder
        });
        failDisburseRes.Status.Should().Be(400);
        var failBody = await failDisburseRes.TextAsync();
        failBody.Should().Contain("Goods must be physically inspected prior to vendor payment");

        // 3. Record Goods Inspection
        var inspRes = await api.PostAsync($"/api/v1/shariah/murabaha/{facility.Id}/inspect-goods", new()
        {
            DataObject = new InspectGoodsRequestDto("INSP-BOILER-VERIFIED-777", "INSPECTOR-HEAD-01")
        });
        inspRes.Status.Should().Be(200);

        // 4. Disburse directly to Verified Vendor
        var disburseRes = await api.PostAsync($"/api/v1/shariah/murabaha/{facility.Id}/disburse-vendor", new()
        {
            DataObject = prematureOrder
        });
        disburseRes.Status.Should().Be(200);

        var disbursedFacility = JsonSerializer.Deserialize<MurabahaFacilityDto>(await disburseRes.TextAsync(), JsonOptions);
        disbursedFacility.Should().NotBeNull();
        disbursedFacility!.Status.Should().Be(ShariahFacilityStatus.DisbursedToVendor);
        disbursedFacility.OutstandingBalance.Should().Be(4480000m); // Cost + Profit
        disbursedFacility.DisbursedAtUtc.Should().NotBeNull();

        // 5. Query facility
        var getRes = await api.GetAsync($"/api/v1/shariah/murabaha/{facility.Id}");
        getRes.Status.Should().Be(200);
    }

    [Fact]
    public async Task Test_Quard_BenevolentZeroInterestFacility()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Create Quard Facility
        var createPayload = new CreateQuardFacilityRequestDto(
            "QRD-E2E-001",
            "CIF-IND-E2E",
            75000m, // Principal
            750m,   // Documented operational cost recovery
            12);

        var createRes = await api.PostAsync("/api/v1/shariah/quard/create", new()
        {
            DataObject = createPayload
        });
        createRes.Status.Should().Be(201);

        var facility = JsonSerializer.Deserialize<QuardFacilityDto>(await createRes.TextAsync(), JsonOptions);
        facility.Should().NotBeNull();
        facility!.PrincipalAmount.Should().Be(75000m);
        facility.InterestRate.Should().Be(0.00m);
        facility.ProfitRate.Should().Be(0.00m);
        facility.IsSettled.Should().BeFalse();

        // 2. Query Quard Facility
        var getRes = await api.GetAsync($"/api/v1/shariah/quard/{facility.Id}");
        getRes.Status.Should().Be(200);
        var fetched = JsonSerializer.Deserialize<QuardFacilityDto>(await getRes.TextAsync(), JsonOptions);
        fetched.Should().NotBeNull();
        fetched!.InterestRate.Should().Be(0.00m);
    }

    [Fact]
    public async Task Test_TaWidh_CompensationRoutingToCharityFund()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Accrue overdue penalty into Shariah Charity Suspense
        var penaltyPayload = new AccrueTaWidhPenaltyRequestDto(
            "MUR-OVERDUE-01",
            "CIF-DEFAULTER-01",
            25000m);

        var accrueRes = await api.PostAsync("/api/v1/shariah/tawidh/accrue", new()
        {
            DataObject = penaltyPayload
        });
        accrueRes.Status.Should().Be(201);

        var record = JsonSerializer.Deserialize<TaWidhRecordDto>(await accrueRes.TextAsync(), JsonOptions);
        record.Should().NotBeNull();
        record!.Status.Should().Be(TaWidhStatus.AccruedInSuspense);
        record.OverduePenaltyAmount.Should().Be(25000m);

        // 2. Check summary (must be in suspense, not bank P&L income)
        var summaryRes = await api.GetAsync("/api/v1/shariah/tawidh/summary");
        summaryRes.Status.Should().Be(200);

        var summary = JsonSerializer.Deserialize<TaWidhFundSummaryDto>(await summaryRes.TextAsync(), JsonOptions);
        summary.Should().NotBeNull();
        summary!.TotalSuspenseBalance.Should().BeGreaterThanOrEqualTo(25000m);

        // 3. Disburse with Shariah Board Approval to approved social cause
        var disburseRes = await api.PostAsync($"/api/v1/shariah/tawidh/{record.Id}/disburse-charity", new()
        {
            DataObject = new DisburseCharityFundRequestDto(
                "Winter Clothes Distribution for Flooded Rural Communities",
                "SHARIAH-CHAIRMAN-01")
        });
        disburseRes.Status.Should().Be(200);

        var disbursedRecord = JsonSerializer.Deserialize<TaWidhRecordDto>(await disburseRes.TextAsync(), JsonOptions);
        disbursedRecord.Should().NotBeNull();
        disbursedRecord!.Status.Should().Be(TaWidhStatus.DisbursedToCharity);
        disbursedRecord.ApprovedSocialWelfareCause.Should().Contain("Winter Clothes");
        disbursedRecord.DisbursedAtUtc.Should().NotBeNull();
    }
}
