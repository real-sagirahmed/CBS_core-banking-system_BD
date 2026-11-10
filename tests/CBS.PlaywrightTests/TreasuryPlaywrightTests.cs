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

    [Fact]
    public async Task PlaywrightE2E_TreasuryDeal_BackOfficeSettlement_GeneratesDoubleEntryGlVoucher_AndDurablePersistence()
    {
        var api = await _fixture.CreateApiContextAsync();
        var uniqueNum = Random.Shared.Next(10000, 99999);
        var dealRef = $"TR-MM-2026-{uniqueNum}";
        var dealDate = DateTime.UtcNow.Date;
        var valueDate = dealDate.AddDays(1);

        // Act 1: Front Office captures Call Money Borrowing deal (50,000,000 BDT from Sonali Bank PLC)
        var captureReq = new CaptureTreasuryDealRequest(
            DealReference: dealRef,
            Type: TreasuryDealType.CallMoneyBorrowing,
            Direction: DealDirection.Borrow,
            CounterpartyBankBic: "BSONBDDH",
            CounterpartyName: "Sonali Bank PLC",
            PrimaryAmount: 50000000m,
            PrimaryCurrency: "BDT",
            SecondaryAmount: 50000000m,
            SecondaryCurrency: "BDT",
            ExchangeRate: 1.00m,
            InterestRate: 9.25m,
            DealDate: dealDate,
            ValueDate: valueDate,
            FrontOfficeDealerUserId: $"DEALER_{uniqueNum}",
            BranchCode: "001");

        var captureRes = await api.PostAsync("/api/v1/treasury/deals/capture", new APIRequestContextOptions
        {
            DataObject = captureReq
        });
        captureRes.Status.Should().Be(201);

        // Act 2: Independent Mid-Office Officer verifies risk limits
        var verifyRes = await api.PostAsync("/api/v1/treasury/deals/verify-mid-office", new APIRequestContextOptions
        {
            DataObject = new VerifyMidOfficeDealRequest(dealRef, $"RISK_OFFICER_{uniqueNum}")
        });
        verifyRes.Status.Should().Be(200);

        // Act 3: Independent Back-Office Officer settles deal -> triggers double-entry GL voucher
        var settleRes = await api.PostAsync("/api/v1/treasury/deals/settle-back-office", new APIRequestContextOptions
        {
            DataObject = new SettleBackOfficeDealRequest(dealRef, $"SETTLEMENT_OFFICER_{uniqueNum}")
        });
        settleRes.Status.Should().Be(200);
        var settledDeal = (await settleRes.JsonAsync())?.Deserialize<TreasuryDealResponse>(JsonOptions);
        settledDeal.Should().NotBeNull();
        settledDeal!.Status.Should().Be("SettledByBackOffice");
        settledDeal.SettlementVoucherNumber.Should().NotBeNullOrWhiteSpace();

        // Act 4: Query Deal from API and verify durable persistence
        var getDealRes = await api.GetAsync($"/api/v1/treasury/deals/{dealRef}");
        getDealRes.Status.Should().Be(200);
        var persistedDeal = (await getDealRes.JsonAsync())?.Deserialize<TreasuryDealResponse>(JsonOptions);
        persistedDeal.Should().NotBeNull();
        persistedDeal!.Status.Should().Be("SettledByBackOffice");
        persistedDeal.SettlementVoucherNumber.Should().Be(settledDeal.SettlementVoucherNumber);

        // Act 5: Query GL Voucher and verify balanced double-entry booking (BRPD Module 1.3 & 6)
        var voucherRes = await api.GetAsync($"/api/v1/gl/vouchers/{settledDeal.SettlementVoucherNumber}");
        voucherRes.Status.Should().Be(200);
        var voucherJson = await voucherRes.TextAsync();
        voucherJson.Should().Contain("102010000"); // Dr Bangladesh Bank Settlement
        voucherJson.Should().Contain("205010000"); // Cr Call Money Borrowings
    }

    [Fact]
    public async Task PlaywrightE2E_TreasurySecurities_MtmRevaluationBatch_PostsRevaluationReserveGlVouchers()
    {
        var api = await _fixture.CreateApiContextAsync();
        var uniqueNum = Random.Shared.Next(10000, 99999);
        var tbillIsin = $"BD09260{uniqueNum}";
        var tbondIsin = $"BD09361{uniqueNum}";
        var issueDate = DateTime.UtcNow.Date.AddDays(-10);

        // Act 1: Register HFT 91-Day Treasury Bill (100,000,000 BDT Face Value, 97,500,000 BDT Book Value)
        var regHftReq = new RegisterTreasurySecurityRequest(
            SecurityId: tbillIsin,
            SecurityName: "91-Day Bangladesh Government Treasury Bill",
            SecurityType: TreasurySecurityType.TreasuryBill91D,
            Classification: SecurityClassification.Hft,
            FaceValue: 100000000m,
            BookValue: 97500000m,
            Currency: "BDT",
            CouponRate: 0.00m,
            PurchaseYield: 10.25m,
            IssueDate: issueDate,
            MaturityDate: issueDate.AddDays(91),
            BranchCode: "001");

        var regHftRes = await api.PostAsync("/api/v1/treasury/securities/register", new APIRequestContextOptions
        {
            DataObject = regHftReq
        });
        regHftRes.Status.Should().Be(201);

        // Act 2: Register HTM 10-Year Treasury Bond (200,000,000 BDT)
        var regHtmReq = new RegisterTreasurySecurityRequest(
            SecurityId: tbondIsin,
            SecurityName: "10-Year Bangladesh Government Treasury Bond",
            SecurityType: TreasurySecurityType.TreasuryBond10Y,
            Classification: SecurityClassification.Htm,
            FaceValue: 200000000m,
            BookValue: 200000000m,
            Currency: "BDT",
            CouponRate: 12.50m,
            PurchaseYield: 12.50m,
            IssueDate: issueDate,
            MaturityDate: issueDate.AddYears(10),
            BranchCode: "001");

        var regHtmRes = await api.PostAsync("/api/v1/treasury/securities/register", new APIRequestContextOptions
        {
            DataObject = regHtmReq
        });
        regHtmRes.Status.Should().Be(201);

        // Act 3: Execute Daily MTM Revaluation Batch (clean price rises to 98.20 -> 700k BDT Gain)
        var mtmBatchReq = new MtmRevaluationBatchRequest(
            BranchCode: "001",
            ValuerUserId: $"VALUER_{uniqueNum}",
            Quotes: new List<SecurityMarketQuoteDto>
            {
                new(tbillIsin, 9.50m, 98.20m)
            });

        var mtmRes = await api.PostAsync("/api/v1/treasury/securities/mtm-batch", new APIRequestContextOptions
        {
            DataObject = mtmBatchReq
        });
        mtmRes.Status.Should().Be(200);
        var batchResult = (await mtmRes.JsonAsync())?.Deserialize<MtmRevaluationBatchResponseDto>(JsonOptions);
        batchResult.Should().NotBeNull();
        batchResult!.TotalHftSecuritiesEvaluated.Should().BeGreaterOrEqualTo(1);
        batchResult.TotalRevaluationVouchersPosted.Should().BeGreaterOrEqualTo(1);

        var billResult = batchResult.Results.FirstOrDefault(r => r.SecurityId == tbillIsin);
        billResult.Should().NotBeNull();
        billResult!.MtmDifferential.Should().Be(700000m);
        billResult.VoucherNumber.Should().NotBeNullOrWhiteSpace();

        // Act 4: Query security and verify updated market value and unrealized gain
        var secRes = await api.GetAsync($"/api/v1/treasury/securities/{tbillIsin}");
        secRes.Status.Should().Be(200);
        var secDto = (await secRes.JsonAsync())?.Deserialize<TreasurySecurityResponseDto>(JsonOptions);
        secDto.Should().NotBeNull();
        secDto!.MarketValue.Should().Be(98200000m);
        secDto.UnrealizedMtmGainLoss.Should().Be(700000m);

        // Act 5: Verify MTM GL Voucher posted to Revaluation Reserve (301020000)
        var mtmVoucherRes = await api.GetAsync($"/api/v1/gl/vouchers/{billResult.VoucherNumber}");
        mtmVoucherRes.Status.Should().Be(200);
        var mtmVoucherJson = await mtmVoucherRes.TextAsync();
        mtmVoucherJson.Should().Contain("104010000"); // Dr Treasury Bills (HFT)
        mtmVoucherJson.Should().Contain("301020000"); // Cr Revaluation Reserve (DOS Cir 05/2014)
    }

    [Fact]
    public async Task PlaywrightE2E_TreasuryMarketRisk_ParametricVaR99_AndFxNetOpenPositionEnforcement()
    {
        var api = await _fixture.CreateApiContextAsync();

        // Act 1: 99% Parametric Value at Risk evaluation
        var varReq = new EvaluateParametricVaRRequest(
            PortfolioValue: 1000000000m, // 100 Crore BDT
            AnnualVolatilityPercentage: 12.0m,
            HoldingDays: 1,
            ConfidenceLevel: 0.99m);

        var varRes = await api.PostAsync("/api/v1/treasury/risk/var/evaluate", new APIRequestContextOptions
        {
            DataObject = varReq
        });
        varRes.Status.Should().Be(200);
        var varResult = (await varRes.JsonAsync())?.Deserialize<ParametricVaRResponseDto>(JsonOptions);
        varResult.Should().NotBeNull();
        varResult!.PortfolioValue.Should().Be(1000000000m);
        varResult.ConfidenceLevel.Should().Be(0.99m);
        varResult.ValueAtRiskAmount.Should().BeGreaterThan(0m);
        varResult.ValueAtRiskPercentage.Should().BeGreaterThan(1.0m).And.BeLessThan(3.0m);

        // Act 2: Foreign Exchange Net Open Position (NOP) evaluation
        var nopReq = new FxNetOpenPositionRequest(
            ApprovedNopLimit: 25000000m, // 25M USD approved ceiling
            Positions: new List<FxPositionInputDto>
            {
                new("USD", 30000000m, 10000000m), // Net +20M
                new("EUR", 5000000m, 12000000m),  // Net -7M
                new("GBP", 4000000m, 2000000m)   // Net +2M
            });

        var nopRes = await api.PostAsync("/api/v1/treasury/risk/nop/evaluate", new APIRequestContextOptions
        {
            DataObject = nopReq
        });
        nopRes.Status.Should().Be(200);
        var nopResult = (await nopRes.JsonAsync())?.Deserialize<FxNetOpenPositionResponseDto>(JsonOptions);
        nopResult.Should().NotBeNull();
        nopResult!.OverallNetOpenPosition.Should().Be(22000000m);
        nopResult.HasNopLimitBreach.Should().BeFalse();
        nopResult.UtilizationPercentage.Should().Be(88.00m);
        nopResult.CurrencyBreakdown.Should().HaveCount(3);
    }
}
