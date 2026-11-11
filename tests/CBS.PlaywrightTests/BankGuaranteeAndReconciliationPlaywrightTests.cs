using System.Text.Json;
using CBS.Application.Reconciliation.DTOs;
using CBS.Application.TradeFinance.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

// ARCHITECTURAL INTENT: End-to-End integration tests asserting Bank Guarantees (Module 5.4)
// and Multi-Channel Reconciliation Aging & Provisions (Module 1.4) conforming to BRPD Circular 12/2024.
public class BankGuaranteeAndReconciliationPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public BankGuaranteeAndReconciliationPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Test_BankGuarantee_FullLifecycle_IssueAmendInvokeClaimLiquidate()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Issue Bank Guarantee (BRPD 5.4.1)
        var bgNumber = "BG-0101-2026-9001";
        var issuePayload = new IssueBankGuaranteeRequest(
            bgNumber,
            GuaranteeType.PerformanceBond,
            "CIF-CORP-901",
            "Dhaka Water Supply and Sewerage Authority (WASA)",
            "WASA Bhaban, Karwan Bazar, Dhaka",
            "0101",
            2_000_000m,
            25.0m, // 25% Margin -> 500,000 BDT
            null,
            0.50m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 7, 1), // 181 days + 30 days claim = 211 days -> 3 quarters
            30,
            GuaranteeExpiryType.FixedDate,
            "MAKER-01",
            "CHECKER-02");

        var issueRes = await api.PostAsync("/api/v1/trade/guarantees/issue", new() { DataObject = issuePayload });
        issueRes.Status.Should().Be(201);

        var bg = JsonSerializer.Deserialize<BankGuaranteeResponse>(await issueRes.TextAsync(), JsonOptions);
        bg.Should().NotBeNull();
        bg!.BgNumber.Should().Be(bgNumber);
        bg.GuaranteeAmount.Should().Be(2_000_000m);
        bg.CashMarginAmount.Should().Be(500_000m);
        bg.CommissionAmount.Should().Be(30_000m); // 2,000,000 * 0.005 * 3
        bg.VatAmount.Should().Be(4_500m); // 15% of 30,000
        bg.Status.Should().Be(nameof(BankGuaranteeStatus.Issued));

        // 2. Amend Bank Guarantee (Increase by 500,000 BDT) (BRPD 5.4.1-ii)
        var amendPayload = new AmendBankGuaranteeRequest(
            bgNumber,
            500_000m,
            new DateOnly(2026, 10, 1),
            "AMEND-REF-WASA-CONTRACT-EXT",
            "CHECKER-02");

        var amendRes = await api.PostAsync("/api/v1/trade/guarantees/amend", new() { DataObject = amendPayload });
        amendRes.Status.Should().Be(200);

        var amended = JsonSerializer.Deserialize<BankGuaranteeResponse>(await amendRes.TextAsync(), JsonOptions);
        amended!.GuaranteeAmount.Should().Be(2_500_000m);
        amended.CashMarginAmount.Should().Be(625_000m); // 25% of 2.5M
        amended.Status.Should().Be(nameof(BankGuaranteeStatus.Amended));

        // 3. Beneficiary Invokes Claim on Default (BRPD 5.4.1-iv)
        var claimPayload = new InvokeGuaranteeClaimRequest(
            bgNumber,
            "WASA-DEFAULT-CLAIM-LTR-7788",
            new DateOnly(2026, 8, 15),
            "SUP-01");

        var claimRes = await api.PostAsync("/api/v1/trade/guarantees/invoke-claim", new() { DataObject = claimPayload });
        claimRes.Status.Should().Be(200);

        var invoked = JsonSerializer.Deserialize<BankGuaranteeResponse>(await claimRes.TextAsync(), JsonOptions);
        invoked!.Status.Should().Be(nameof(BankGuaranteeStatus.ClaimInvoked));

        // 4. Liquidate Claim
        var liquidatePayload = new LiquidateGuaranteeClaimRequest(bgNumber, "MGR-01");
        var liquidateRes = await api.PostAsync("/api/v1/trade/guarantees/liquidate-claim", new() { DataObject = liquidatePayload });
        liquidateRes.Status.Should().Be(200);

        var liquidated = JsonSerializer.Deserialize<BankGuaranteeResponse>(await liquidateRes.TextAsync(), JsonOptions);
        liquidated!.Status.Should().Be(nameof(BankGuaranteeStatus.LiquidatedClaimPaid));

        // 5. Query BG Status via GET
        var getRes = await api.GetAsync($"/api/v1/trade/guarantees/{bgNumber}");
        getRes.Status.Should().Be(200);
    }

    [Fact]
    public async Task Test_BankGuarantee_CancelAndRelease()
    {
        var api = await _fixture.CreateApiContextAsync();

        var bgNumber = "BG-0101-2026-9002";
        var issuePayload = new IssueBankGuaranteeRequest(
            bgNumber,
            GuaranteeType.BidBond_Tender,
            "CIF-CORP-902",
            "Rural Electrification Board (REB)",
            "Nikunja-2, Khilkhet, Dhaka",
            "0101",
            1_000_000m,
            10.0m,
            null,
            0.50m,
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 5, 1),
            15,
            GuaranteeExpiryType.FixedDate,
            "MAKER-01",
            "CHECKER-02");

        var issueRes = await api.PostAsync("/api/v1/trade/guarantees/issue", new() { DataObject = issuePayload });
        issueRes.Status.Should().Be(201);

        // Cancel & Release upon return of original BG (BRPD 5.4.1-iii)
        var cancelPayload = new CancelBankGuaranteeRequest(
            bgNumber,
            "REB-RELEASE-MEMO-55",
            true,
            new DateOnly(2026, 4, 20),
            "MGR-01");

        var cancelRes = await api.PostAsync("/api/v1/trade/guarantees/cancel-release", new() { DataObject = cancelPayload });
        cancelRes.Status.Should().Be(200);

        var cancelled = JsonSerializer.Deserialize<BankGuaranteeResponse>(await cancelRes.TextAsync(), JsonOptions);
        cancelled!.Status.Should().Be(nameof(BankGuaranteeStatus.CancelledReleased));
        cancelled.OriginalReturned.Should().BeTrue();
    }

    [Fact]
    public async Task Test_ReconciliationAging_RecordAssessAndResolve()
    {
        var api = await _fixture.CreateApiContextAsync();

        var traceNo = "TR-ATM-RECON-2026-999";
        var recordPayload = new RecordUnreconciledItemRequest(
            "ATM",
            traceNo,
            "0101",
            "NPSB_SWITCH",
            75_000m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 1)); // 59 days old -> Days31To60 -> 10% provision

        var recordRes = await api.PostAsync("/api/v1/reconciliation/aging/record", new() { DataObject = recordPayload });
        recordRes.Status.Should().Be(201);

        var recorded = JsonSerializer.Deserialize<ReconciliationAgingItemResponse>(await recordRes.TextAsync(), JsonOptions);
        recorded.Should().NotBeNull();
        recorded!.Bucket.Should().Be(nameof(ReconciliationAgingBucket.Days31To60));
        recorded.RequiredProvisionPercentage.Should().Be(10.0m);
        recorded.RequiredProvisionAmount.Should().Be(7_500m); // 10% of 75,000

        // Query Aging Summary as of 2026-03-01 (BRPD 1.4.1-ii, iv)
        var summaryRes = await api.GetAsync("/api/v1/reconciliation/aging/summary?assessmentDate=2026-03-01");
        summaryRes.Status.Should().Be(200);

        var summary = JsonSerializer.Deserialize<ReconciliationAgingSummaryResponse>(await summaryRes.TextAsync(), JsonOptions);
        summary.Should().NotBeNull();
        summary!.TotalUnresolvedItems.Should().BeGreaterThanOrEqualTo(1);

        // Resolve Item
        var resolvePayload = new ResolveUnreconciledItemRequest(
            traceNo,
            "ATM-DISPUTE-CREDIT-ADJUSTMENT-88",
            new DateOnly(2026, 3, 5));

        var resolveRes = await api.PostAsync("/api/v1/reconciliation/aging/resolve", new() { DataObject = resolvePayload });
        resolveRes.Status.Should().Be(200);

        var resolved = JsonSerializer.Deserialize<ReconciliationAgingItemResponse>(await resolveRes.TextAsync(), JsonOptions);
        resolved!.IsResolved.Should().BeTrue();
        resolved.RequiredProvisionAmount.Should().Be(0m);
    }
}
