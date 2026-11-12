// ARCHITECTURAL INTENT: Comprehensive Playwright API Integration tests locking
// Treasury Management, FX Blotter, Net Open Position (NOP), Dealer & Counterparty Limits,
// Mark-to-Market (MTM) Revaluation, Nostro Mirror Reconciliation, and Statutory Reserves (CRR/SLR)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 6, DOS Circular No. 05/2008, and MPD Circular No. 03/2020.

using System.Text.Json;
using CBS.Application.Treasury.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class TreasuryManagementAndReservePlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public TreasuryManagementAndReservePlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetFxBlotter_Should_Return_Currency_Positions_And_Nop_Compliance()
    {
        // ARCHITECTURAL INTENT: Real-time FX Blotter summary aggregates all currency positions
        // and checks against Bangladesh Bank approved NOP ceiling [BRPD 6.1.1, 6.1.2]
        var api = await _fixture.CreateApiContextAsync();

        var res = await api.GetAsync("/api/v1/treasury/blotter");
        res.Status.Should().Be(200);

        var blotter = (await res.JsonAsync())?.Deserialize<FxBlotterSummaryDto>(JsonOptions);
        blotter.Should().NotBeNull();
        blotter!.CurrencyPositions.Should().NotBeNull();
        blotter.CurrencyPositions.Should().Contain(c => c.Currency == "USD");
        blotter.SanctionedNopLimitUsd.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task CheckTreasuryLimit_Should_Enforce_Sanctioned_Ceilings()
    {
        // ARCHITECTURAL INTENT: Dealer & Counterparty limit checks enforce regulatory limits [BRPD 6.2.2, 6.3.1]
        var api = await _fixture.CreateApiContextAsync();

        // 1. Configure limit profile for Dealer
        var profileCmd = new ConfigureTreasuryLimitProfileCommand(
            LimitType: TreasuryLimitType.DealerDaylight,
            Identifier: "DEALER-001",
            Currency: "USD",
            SanctionedLimit: 1_000_000m,
            ApprovedByUserId: "HEAD-OF-TREASURY",
            EffectiveDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        var profileRes = await api.PostAsync("/api/v1/treasury/limits/profile", new APIRequestContextOptions
        {
            DataObject = profileCmd
        });
        profileRes.Status.Should().Be(200);

        // 2. Within limit check (500,000 USD out of 1,000,000 USD)
        var command = new CheckTreasuryLimitCommand(
            LimitType: TreasuryLimitType.DealerDaylight,
            Identifier: "DEALER-001",
            DealReference: "DEAL-TEST-001",
            Amount: 500_000m,
            Currency: "USD"
        );

        var res = await api.PostAsync("/api/v1/treasury/limits/check", new APIRequestContextOptions
        {
            DataObject = command
        });
        res.Status.Should().Be(200);

        var result = (await res.JsonAsync())?.Deserialize<TreasuryLimitCheckResultDto>(JsonOptions);
        result.Should().NotBeNull();
        result!.IsAllowed.Should().BeTrue();
        result.SanctionedLimit.Should().Be(1_000_000m);
        result.UtilizedAmount.Should().Be(500_000m);
        result.AvailableAmount.Should().Be(500_000m);

        // 3. Exceeding limit check (600,000 USD when only 500,000 USD available) -> Not allowed
        var exceedCmd = new CheckTreasuryLimitCommand(
            LimitType: TreasuryLimitType.DealerDaylight,
            Identifier: "DEALER-001",
            DealReference: "DEAL-TEST-002",
            Amount: 600_000m,
            Currency: "USD"
        );

        var exceedRes = await api.PostAsync("/api/v1/treasury/limits/check", new APIRequestContextOptions
        {
            DataObject = exceedCmd
        });
        exceedRes.Status.Should().Be(200);

        var exceedResult = (await exceedRes.JsonAsync())?.Deserialize<TreasuryLimitCheckResultDto>(JsonOptions);
        exceedResult.Should().NotBeNull();
        exceedResult!.IsAllowed.Should().BeFalse();
        exceedResult.Message.Should().Contain("breaches sanctioned limit");
    }

    [Fact]
    public async Task ExecuteMtmRevaluation_Should_Process_Batch_And_Return_Reserve()
    {
        // ARCHITECTURAL INTENT: Daily EOD MTM Revaluation for FX Forwards and HFT Securities [BRPD 6.3.1-v, DOS 05/2008]
        var api = await _fixture.CreateApiContextAsync();

        var command = new ExecuteMtmRevaluationCommand(
            RevaluationDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ExecutedByUserId: "TREASURY-ANALYST-01"
        );

        var res = await api.PostAsync("/api/v1/treasury/revaluation/mtm-eod", new APIRequestContextOptions
        {
            DataObject = command
        });
        res.Status.Should().Be(200);

        var batch = (await res.JsonAsync())?.Deserialize<MtmRevaluationResultDto>(JsonOptions);
        batch.Should().NotBeNull();
        batch!.Status.Should().Be("Completed");
        batch.BatchReference.Should().StartWith("MTM-");
    }

    [Fact]
    public async Task EvaluateStatutoryReserves_Conventional_Should_Enforce_CRR_350_And_SLR_1300()
    {
        // ARCHITECTURAL INTENT: CRR and SLR regulatory assessment per Bangladesh Bank MPD Circular No. 03/2020
        var api = await _fixture.CreateApiContextAsync();

        var command = new EvaluateStatutoryReservesCommand(
            AssessmentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            BankingType: BankingType.Conventional,
            TdtlBaseBdt: 10_000_000_000m,
            MaintainedCrrCashBdt: 380_000_000m, // 3.80% (compliant with 3.50% daily)
            MaintainedSlrAssetsBdt: 1_350_000_000m, // 13.50% (compliant with 13.00%)
            EvaluatedByUserId: "RISK-OFFICER-01"
        );

        var res = await api.PostAsync("/api/v1/treasury/reserves/evaluate-crr-slr", new APIRequestContextOptions
        {
            DataObject = command
        });
        res.Status.Should().Be(200);

        var assessment = (await res.JsonAsync())?.Deserialize<StatutoryReserveAssessmentDto>(JsonOptions);
        assessment.Should().NotBeNull();
        assessment!.IsDailyCrrCompliant.Should().BeTrue();
        assessment.DailyCrrShortfallBdt.Should().Be(0m);
        assessment.RequiredCrrDailyBdt.Should().Be(350_000_000m); // 3.50%

        assessment.IsSlrCompliant.Should().BeTrue();
        assessment.SlrShortfallBdt.Should().Be(0m);
        assessment.RequiredSlrBdt.Should().Be(1_300_000_000m); // 13.00%

        // Verify GET compliance statement endpoint
        var stmtRes = await api.GetAsync("/api/v1/treasury/reserves/compliance-statement");
        stmtRes.Status.Should().Be(200);
        var stmt = (await stmtRes.JsonAsync())?.Deserialize<StatutoryReserveAssessmentDto>(JsonOptions);
        stmt.Should().NotBeNull();
        stmt!.TdtlBaseBdt.Should().Be(10_000_000_000m);
    }

    [Fact]
    public async Task EvaluateStatutoryReserves_Islamic_Should_Enforce_SLR_550()
    {
        // ARCHITECTURAL INTENT: Islamic Banks SLR requirement is 5.50% of TDTL [MPD Circular 03/2020]
        var api = await _fixture.CreateApiContextAsync();

        var command = new EvaluateStatutoryReservesCommand(
            AssessmentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            BankingType: BankingType.Islamic,
            TdtlBaseBdt: 5_000_000_000m,
            MaintainedCrrCashBdt: 200_000_000m,
            MaintainedSlrAssetsBdt: 300_000_000m, // 6.00% (compliant with 5.50%)
            EvaluatedByUserId: "ISLAMIC-RISK-01"
        );

        var res = await api.PostAsync("/api/v1/treasury/reserves/evaluate-crr-slr", new APIRequestContextOptions
        {
            DataObject = command
        });
        res.Status.Should().Be(200);

        var assessment = (await res.JsonAsync())?.Deserialize<StatutoryReserveAssessmentDto>(JsonOptions);
        assessment.Should().NotBeNull();
        assessment!.RequiredSlrBdt.Should().Be(275_000_000m); // 5.50% of 5,000,000,000
        assessment.IsSlrCompliant.Should().BeTrue();
    }

    [Fact]
    public async Task NostroReconciliation_Lifecycle_Should_Ingest_AutoMatch_And_Report_Aging()
    {
        // ARCHITECTURAL INTENT: End-to-end Nostro Mirror Reconciliation lifecycle:
        // Local entry ingestion -> MT950 Statement matching -> Un-reconciled debit aging report [BRPD 6.4.2]
        var api = await _fixture.CreateApiContextAsync();
        string testRef = $"FT-TEST-{Random.Shared.Next(100000, 999999)}";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Ingest local entry
        var ingestCmd = new IngestLocalNostroEntryCommand(
            NostroAccountCode: "NOSTRO-USD-CITI",
            Currency: "USD",
            ValueDate: today,
            EntryType: "Debit",
            LocalBookReference: testRef,
            LocalBookAmount: 150_000.00m
        );

        var ingestRes = await api.PostAsync("/api/v1/treasury/nostro/local-entry", new APIRequestContextOptions
        {
            DataObject = ingestCmd
        });
        ingestRes.Status.Should().Be(200);

        // 2. Auto-reconcile with correspondent statement matching this entry
        var reconcileCmd = new IngestNostroStatementCommand(
            NostroAccountCode: "NOSTRO-USD-CITI",
            Currency: "USD",
            Entries: new List<NostroStatementEntryDto>
            {
                new(
                    StatementReference: testRef,
                    ValueDate: today,
                    EntryType: "Debit",
                    Amount: 150_000.00m
                )
            }
        );

        var reconcileRes = await api.PostAsync("/api/v1/treasury/nostro/reconcile-auto", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "NOSTRO-OPERATOR-01" } },
            DataObject = reconcileCmd
        });
        reconcileRes.Status.Should().Be(200);

        var batchResult = (await reconcileRes.JsonAsync())?.Deserialize<NostroReconciliationBatchResultDto>(JsonOptions);
        batchResult.Should().NotBeNull();
        batchResult!.AutoMatchedCount.Should().BeGreaterThanOrEqualTo(1);

        // 3. Retrieve aging report
        var agingRes = await api.GetAsync("/api/v1/treasury/nostro/unreconciled-aging");
        agingRes.Status.Should().Be(200);

        var agingReport = (await agingRes.JsonAsync())?.Deserialize<NostroUnreconciledAgingReportDto>(JsonOptions);
        agingReport.Should().NotBeNull();
        agingReport!.TotalRequiredProvisionAmount.Should().BeGreaterThanOrEqualTo(0m);
    }
}
