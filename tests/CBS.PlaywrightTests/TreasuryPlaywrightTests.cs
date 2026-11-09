using System.Text.Json;
using CBS.Application.Treasury.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class TreasuryPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public TreasuryPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PlaywrightE2E_TreasuryDeal_ThreeTierDutySegregation_FrontOffice_MidOffice_BackOffice()
    {
        var api = await _fixture.CreateApiContextAsync();
        var dealRef = $"TR-FX-2026-{Random.Shared.Next(1000, 9999)}";
        var dealDate = new DateTime(2026, 7, 1);
        var valueDate = new DateTime(2026, 7, 3);

        // Act 1: Front Office captures FX Spot Deal (Buy 2,000,000 USD against 240,000,000 BDT)
        var captureReq = new CaptureTreasuryDealRequest(
            DealReference: dealRef,
            Type: TreasuryDealType.FxSpot,
            Direction: DealDirection.Buy,
            CounterpartyBankBic: "HSBCBDDH",
            CounterpartyName: "HSBC Bangladesh",
            PrimaryAmount: 2000000m,
            PrimaryCurrency: "USD",
            SecondaryAmount: 240000000m,
            SecondaryCurrency: "BDT",
            ExchangeRate: 120.00m,
            InterestRate: 0m,
            DealDate: dealDate,
            ValueDate: valueDate,
            FrontOfficeDealerUserId: "DEALER_FO_01");

        var captureRes = await api.PostAsync("/api/v1/treasury/deals/capture", new APIRequestContextOptions
        {
            DataObject = captureReq
        });
        captureRes.Status.Should().Be(201);
        var capturedDeal = (await captureRes.JsonAsync())?.Deserialize<TreasuryDealResponse>(JsonOptions);
        capturedDeal.Should().NotBeNull();
        capturedDeal!.Status.Should().Be("CapturedByFrontOffice");

        // Act 2: Dealer attempts to verify own deal in Mid Office (BRPD 6.5.1-ii segregation violation)
        var selfVerifyRes = await api.PostAsync("/api/v1/treasury/deals/verify-mid-office", new APIRequestContextOptions
        {
            DataObject = new VerifyMidOfficeDealRequest(dealRef, "DEALER_FO_01")
        });
        selfVerifyRes.Status.Should().Be(400);
        var selfVerifyError = await selfVerifyRes.TextAsync();
        selfVerifyError.Should().Contain("Front-office dealer cannot perform mid-office risk and limit verification");

        // Act 3: Independent Mid Office Risk Officer verifies deal
        var midVerifyRes = await api.PostAsync("/api/v1/treasury/deals/verify-mid-office", new APIRequestContextOptions
        {
            DataObject = new VerifyMidOfficeDealRequest(dealRef, "RISK_OFFICER_MO_01")
        });
        midVerifyRes.Status.Should().Be(200);
        var verifiedDeal = (await midVerifyRes.JsonAsync())?.Deserialize<TreasuryDealResponse>(JsonOptions);
        verifiedDeal.Should().NotBeNull();
        verifiedDeal!.Status.Should().Be("VerifiedByMidOffice");
        verifiedDeal.MidOfficeRiskOfficerUserId.Should().Be("RISK_OFFICER_MO_01");

        // Act 4: Mid Office Risk Officer attempts Back Office settlement (BRPD 6.5.4 segregation violation)
        var midSettleRes = await api.PostAsync("/api/v1/treasury/deals/settle-back-office", new APIRequestContextOptions
        {
            DataObject = new SettleBackOfficeDealRequest(dealRef, "RISK_OFFICER_MO_01")
        });
        midSettleRes.Status.Should().Be(400);
        var midSettleError = await midSettleRes.TextAsync();
        midSettleError.Should().Contain("Mid-office risk officer strictly prohibited from executing back-office settlement");

        // Act 5: Independent Back Office Officer executes settlement
        var backSettleRes = await api.PostAsync("/api/v1/treasury/deals/settle-back-office", new APIRequestContextOptions
        {
            DataObject = new SettleBackOfficeDealRequest(dealRef, "SETTLEMENT_BO_01")
        });
        backSettleRes.Status.Should().Be(200);
        var settledDeal = (await backSettleRes.JsonAsync())?.Deserialize<TreasuryDealResponse>(JsonOptions);
        settledDeal.Should().NotBeNull();
        settledDeal!.Status.Should().Be("SettledByBackOffice");
        settledDeal.BackOfficeSettlementUserId.Should().Be("SETTLEMENT_BO_01");
        settledDeal.SettledAtUtc.Should().NotBeNull();

        // Act 6: Query Deal and verify 3-tier audit segregation trail
        var getRes = await api.GetAsync($"/api/v1/treasury/deals/{dealRef}");
        getRes.Status.Should().Be(200);
        var fetchedDeal = (await getRes.JsonAsync())?.Deserialize<TreasuryDealResponse>(JsonOptions);
        fetchedDeal!.FrontOfficeDealerUserId.Should().Be("DEALER_FO_01");
        fetchedDeal.MidOfficeRiskOfficerUserId.Should().Be("RISK_OFFICER_MO_01");
        fetchedDeal.BackOfficeSettlementUserId.Should().Be("SETTLEMENT_BO_01");
    }

    [Fact]
    public async Task PlaywrightE2E_TreasuryAlm_CrrSlrMonitoring_DetectsShortfallForConventionalAndIslamicBanks()
    {
        var api = await _fixture.CreateApiContextAsync();

        // Act 1: Conventional Bank position check with CRR shortfall
        // NDTL: 5,000,000,000 BDT (500 Crore)
        // 3.50% min daily CRR = 175,000,000 BDT
        // Maintained CRR: 150,000,000 BDT (3.00%) -> 25,000,000 BDT Shortfall!
        // 13.00% SLR = 650,000,000 BDT
        // Maintained SLR: 700,000,000 BDT (14.00%) -> Surplus
        var convReq = new EvaluateCrrSlrRequest(
            NdtlAmount: 5000000000m,
            Currency: "BDT",
            BankingType: BankingType.Conventional,
            MaintainedCrrCash: 150000000m,
            MaintainedSlrSecurities: 700000000m);

        var convRes = await api.PostAsync("/api/v1/treasury/alm/crr-slr/evaluate", new APIRequestContextOptions
        {
            DataObject = convReq
        });
        convRes.Status.Should().Be(200);
        var convResult = (await convRes.JsonAsync())?.Deserialize<CrrSlrResponse>(JsonOptions);
        convResult.Should().NotBeNull();
        convResult!.HasCrrShortfall.Should().BeTrue();
        convResult.ActualCrrPercentage.Should().Be(3.00m);
        convResult.CrrShortfallOrSurplusAmount.Should().Be(-25000000m);
        convResult.HasSlrShortfall.Should().BeFalse();
        convResult.ActualSlrPercentage.Should().Be(14.00m);

        // Act 2: Islamic Bank position check
        // NDTL: 5,000,000,000 BDT
        // Islamic SLR requirement is 5.50% = 275,000,000 BDT
        // Maintained SLR: 300,000,000 BDT (6.00%)
        // Maintained CRR: 200,000,000 BDT (4.00%)
        var islamicReq = new EvaluateCrrSlrRequest(
            NdtlAmount: 5000000000m,
            Currency: "BDT",
            BankingType: BankingType.Islamic,
            MaintainedCrrCash: 200000000m,
            MaintainedSlrSecurities: 300000000m);

        var islamicRes = await api.PostAsync("/api/v1/treasury/alm/crr-slr/evaluate", new APIRequestContextOptions
        {
            DataObject = islamicReq
        });
        islamicRes.Status.Should().Be(200);
        var islamicResult = (await islamicRes.JsonAsync())?.Deserialize<CrrSlrResponse>(JsonOptions);
        islamicResult.Should().NotBeNull();
        islamicResult!.HasCrrShortfall.Should().BeFalse();
        islamicResult.HasSlrShortfall.Should().BeFalse();
        islamicResult.SlrRequiredRate.Should().Be(5.50m);
        islamicResult.ActualSlrPercentage.Should().Be(6.00m);
    }

    [Fact]
    public async Task PlaywrightE2E_TreasuryAlm_StructuralLiquidityGap_DetectsPrudentialAlertOnShortTermDeficit()
    {
        var api = await _fixture.CreateApiContextAsync();

        // Act: Evaluate liquidity gap where 1-14 days bucket has negative net gap exceeding 10% of outflows
        // Days1To14: Inflow 400M, Outflow 700M -> Net gap -300M (Deficit ratio = 300 / 700 = 42.8% > 10%)
        var gapReq = new EvaluateLiquidityGapRequest(
            Currency: "BDT",
            Buckets: new List<LiquidityBucketInput>
            {
                new(LiquidityBucket.Days1To14, 400000000m, 700000000m),
                new(LiquidityBucket.Days15To28, 500000000m, 450000000m),
                new(LiquidityBucket.Days29To90, 800000000m, 600000000m),
                new(LiquidityBucket.Days91To180, 1000000000m, 900000000m),
                new(LiquidityBucket.Days181To365, 1200000000m, 1100000000m),
                new(LiquidityBucket.Over1Year, 2000000000m, 1800000000m)
            });

        var gapRes = await api.PostAsync("/api/v1/treasury/alm/liquidity-gap/evaluate", new APIRequestContextOptions
        {
            DataObject = gapReq
        });
        gapRes.Status.Should().Be(200);
        var gapResult = (await gapRes.JsonAsync())?.Deserialize<LiquidityGapResponse>(JsonOptions);
        gapResult.Should().NotBeNull();
        gapResult!.HasShortTermDeficitAlert.Should().BeTrue();
        gapResult.Summary.Should().Contain("Prudential Alert: 1-14 days structural liquidity deficit");
        gapResult.Buckets.Should().HaveCount(6);
        gapResult.Buckets.First().NetGap.Should().Be(-300000000m);
    }
}
