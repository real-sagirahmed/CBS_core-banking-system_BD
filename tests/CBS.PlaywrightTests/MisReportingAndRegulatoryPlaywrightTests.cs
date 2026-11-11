using System.Text.Json;
using CBS.Application.Reporting.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class MisReportingAndRegulatoryPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public MisReportingAndRegulatoryPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Test_DailyCleanCash_And_DailyAffairs_Reporting()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Daily Clean Cash (BRPD 11.1.1-a)
        var cashRes = await api.GetAsync("/api/v1/mis/reports/daily-clean-cash?branchCode=0101");
        cashRes.Status.Should().Be(200);

        var cashBody = await cashRes.TextAsync();
        var cashReport = JsonSerializer.Deserialize<DailyCleanCashReportDto>(cashBody, JsonOptions);
        cashReport.Should().NotBeNull();
        cashReport!.ClosingVaultCash.Should().BeGreaterThan(0m);
        cashReport.TotalClosingCashInHand.Should().BeGreaterThan(0m);
        cashReport.BranchSummaries.Should().NotBeEmpty();

        // 2. Daily Affairs Balance Sheet (BRPD 11.1.1-b)
        var affairsRes = await api.GetAsync("/api/v1/mis/reports/daily-affairs?branchCode=0101");
        affairsRes.Status.Should().Be(200);

        var affairsBody = await affairsRes.TextAsync();
        var affairsReport = JsonSerializer.Deserialize<DailyAffairsReportDto>(affairsBody, JsonOptions);
        affairsReport.Should().NotBeNull();
        affairsReport!.TotalAssets.Should().BeGreaterThan(0m);
        affairsReport.BalanceSheetItems.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Test_DepositAnalytics_Slabs_And_Mix()
    {
        var api = await _fixture.CreateApiContextAsync();

        var res = await api.GetAsync("/api/v1/mis/reports/deposit-analytics?asOfDate=2026-06-30");
        res.Status.Should().Be(200);

        var body = await res.TextAsync();
        var report = JsonSerializer.Deserialize<DepositAnalyticsReportDto>(body, JsonOptions);
        report.Should().NotBeNull();
        report!.Slabs.Should().NotBeEmpty();
        report.MixBreakdowns.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Test_UnclaimedDeposits_10YearLifecycle_NoticeAndTransferToBB()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Scan for 10-year inoperative deposits
        var scanRes = await api.PostAsync("/api/v1/mis/reports/unclaimed-deposits/scan?asOfDate=2026-06-30");
        scanRes.Status.Should().Be(200);

        var scanBody = await scanRes.TextAsync();
        var scanResult = JsonSerializer.Deserialize<ScanUnclaimedDepositsResponseDto>(scanBody, JsonOptions);
        scanResult.Should().NotBeNull();

        // If candidates found or open accounts exist, test notice & transfer endpoints
        if (scanResult!.Candidates.Count > 0)
        {
            var target = scanResult.Candidates[0];

            // 2. Issue statutory 3-month notice
            var noticeReq = new IssueNoticeRequestDto(target.AccountNumber, "STAT-NOTICE-2026/001", new DateOnly(2026, 1, 1));
            var noticeRes = await api.PostAsync("/api/v1/mis/reports/unclaimed-deposits/notice", new APIRequestContextOptions
            {
                DataObject = noticeReq
            });
            noticeRes.Status.Should().Be(200);

            // 3. Remit to Central Bank after statutory window (e.g. remittance in May 2026)
            var transferReq = new TransferUnclaimedDepositsRequestDto(
                new List<string> { target.AccountNumber },
                "SCH-BB-2026-01",
                "BB-RCPT-2026-9901",
                new DateOnly(2026, 5, 15),
                "COMPLIANCE_HEAD_01");

            var transferRes = await api.PostAsync("/api/v1/mis/reports/unclaimed-deposits/transfer-to-bb", new APIRequestContextOptions
            {
                DataObject = transferReq
            });
            transferRes.Status.Should().Be(200);

            var transferBody = await transferRes.TextAsync();
            var transferResult = JsonSerializer.Deserialize<TransferUnclaimedDepositsResponseDto>(transferBody, JsonOptions);
            transferResult.Should().NotBeNull();
            transferResult!.AccountsTransferredCount.Should().Be(1);
            transferResult.TransferredAccountNumbers.Should().Contain(target.AccountNumber);
        }
    }

    [Fact]
    public async Task Test_Sbs3Classification_LoansAndProvisions()
    {
        var api = await _fixture.CreateApiContextAsync();

        var res = await api.GetAsync("/api/v1/mis/reports/sbs3-classification?asOfDate=2026-06-30");
        res.Status.Should().Be(200);

        var body = await res.TextAsync();
        var report = JsonSerializer.Deserialize<Sbs3ClassificationReportDto>(body, JsonOptions);
        report.Should().NotBeNull();
        report!.Summary.Should().NotBeNull();
    }

    [Fact]
    public async Task Test_ForeignTrade_And_RemittanceIncentive_Reporting()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Foreign trade summary (BRPD 11.1.4)
        var ftRes = await api.GetAsync("/api/v1/mis/reports/foreign-trade-summary?asOfDate=2026-06-30");
        ftRes.Status.Should().Be(200);

        var ftBody = await ftRes.TextAsync();
        var ftReport = JsonSerializer.Deserialize<ForeignTradeSummaryReportDto>(ftBody, JsonOptions);
        ftReport.Should().NotBeNull();
        ftReport!.Breakdown.Should().NotBeEmpty();

        // 2. Remittance 2.5% Cash Incentive (BRPD 11.1.5)
        var remRes = await api.GetAsync("/api/v1/mis/reports/remittance-incentives?fromDate=2026-01-01&toDate=2026-12-31");
        remRes.Status.Should().Be(200);

        var remBody = await remRes.TextAsync();
        var remReport = JsonSerializer.Deserialize<RemittanceIncentiveReportDto>(remBody, JsonOptions);
        remReport.Should().NotBeNull();

        // 3. ADC Reconciliation (BRPD 11.1.6)
        var adcRes = await api.GetAsync("/api/v1/mis/reports/adc-reconciliation?date=2026-06-30");
        adcRes.Status.Should().Be(200);

        var adcBody = await adcRes.TextAsync();
        var adcReport = JsonSerializer.Deserialize<AdcReconciliationReportDto>(adcBody, JsonOptions);
        adcReport.Should().NotBeNull();
        adcReport!.Channels.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Test_StatutoryReportArchive_SealSubmitAndRetrieve()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Cryptographically seal report (BRPD 11.2)
        var sealReq = new SealReportRequestDto(
            RegulatoryReportType.Sbs3LoanClassification,
            new DateOnly(2026, 6, 30),
            "0101",
            "COMPLIANCE_HEAD_01",
            ReportExportFormat.Json,
            "{\"reportType\":\"SBS-3\",\"quarter\":\"Q2-2026\",\"nplRatio\":3.45}",
            120);

        var sealRes = await api.PostAsync("/api/v1/mis/reports/archives/seal", new APIRequestContextOptions
        {
            DataObject = sealReq
        });
        sealRes.Status.Should().Be(201);

        var sealBody = await sealRes.TextAsync();
        var sealedSummary = JsonSerializer.Deserialize<PreservedReportSummaryDto>(sealBody, JsonOptions);
        sealedSummary.Should().NotBeNull();
        sealedSummary!.ContentHash.Length.Should().Be(64);
        sealedSummary.IsIntegrityVerified.Should().BeTrue();
        sealedSummary.IsSubmitted.Should().BeFalse();

        // 2. Submit to Regulator
        var submitReq = new SubmitReportRequestDto("BB/STAT/2026/Q2/SBS3-8899", "REG_OFFICER_01");
        var submitRes = await api.PostAsync($"/api/v1/mis/reports/archives/{sealedSummary.Id}/submit", new APIRequestContextOptions
        {
            DataObject = submitReq
        });
        submitRes.Status.Should().Be(200);

        // 3. Retrieve archived report and verify sealed integrity
        var getRes = await api.GetAsync($"/api/v1/mis/reports/archives/{sealedSummary.Id}");
        getRes.Status.Should().Be(200);

        var getBody = await getRes.TextAsync();
        using var doc = JsonDocument.Parse(getBody);
        doc.RootElement.GetProperty("summary").GetProperty("isSubmitted").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("summary").GetProperty("submissionReference").GetString().Should().Be("BB/STAT/2026/Q2/SBS3-8899");
        doc.RootElement.GetProperty("summary").GetProperty("isIntegrityVerified").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("payload").GetString().Should().Contain("Q2-2026");
    }
}
