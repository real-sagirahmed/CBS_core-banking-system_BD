using System.Text.Json;
using CBS.Application.BranchCash.DTOs;
using CBS.Application.Lending.Demand.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

// ARCHITECTURAL INTENT: End-to-End integration tests asserting Demand Loans, Forced PAD,
// Security Deposit Receipts (SDR), and Stop Payment Revocation conforming to BRPD Circular 12/2024 Modules 4.5 & 2.1.
public class DemandLoansAndSdrPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public DemandLoansAndSdrPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Test_DemandLoan_FullLifecycle_InitiateDisburseAccrueCapitalizeRepay()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Initiate Demand Loan (BRPD 4.5.1)
        var loanAcc = AccountNumber.Generate("0101", "451", 20001).Value;
        var initPayload = new InitiateDemandLoanRequest(
            loanAcc,
            "CIF-DEMAND-1001",
            DemandLoanType.PaymentAgainstDocuments_PAD,
            "451",
            "PAD-LC-9901-INV-01",
            "SEC-TRADE-IMP",
            "IND-COMMERCIAL",
            "0101",
            1_000_000m,
            12.0m,
            2.0m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30),
            RepaymentPriorityScheme.Standard);

        var initRes = await api.PostAsync("/api/v1/demand-loans/initiate", new() { DataObject = initPayload });
        initRes.Status.Should().Be(201);

        var facility = JsonSerializer.Deserialize<DemandLoanResponse>(await initRes.TextAsync(), JsonOptions);
        facility.Should().NotBeNull();
        facility!.Status.Should().Be(nameof(DemandLoanStatus.Sanctioned));
        facility.ProductCode.Should().Be("451");

        // 2. Disburse Demand Loan
        var disbursePayload = new DisburseDemandLoanRequest(loanAcc, 500_000m, new DateOnly(2026, 1, 5), "LC Bill Payment");
        var disburseRes = await api.PostAsync("/api/v1/demand-loans/disburse", new() { DataObject = disbursePayload });
        disburseRes.Status.Should().Be(200);

        var disbursed = JsonSerializer.Deserialize<DemandLoanResponse>(await disburseRes.TextAsync(), JsonOptions);
        disbursed!.OutstandingPrincipal.Should().Be(500_000m);
        disbursed.Status.Should().Be(nameof(DemandLoanStatus.Disbursed));

        // 3. Accrue Daily Interest (BRPD 4.5.2-i)
        var accruePayload = new AccrueDailyInterestRequest(loanAcc, new DateOnly(2026, 1, 6));
        var accrueRes = await api.PostAsync("/api/v1/demand-loans/accrue-daily", new() { DataObject = accruePayload });
        accrueRes.Status.Should().Be(200);

        var accrued = JsonSerializer.Deserialize<DemandLoanResponse>(await accrueRes.TextAsync(), JsonOptions);
        accrued!.RegularAccruedInterest.Should().Be(164.38m);

        // 4. Capitalize Interest Schedule (BRPD 4.5.2-iii)
        var capPayload = new CapitalizeInterestRequest(loanAcc, "BB-RESTRUCTURING-APPROVAL-001", new DateOnly(2026, 1, 10));
        var capRes = await api.PostAsync("/api/v1/demand-loans/capitalize-interest", new() { DataObject = capPayload });
        capRes.Status.Should().Be(200);

        var capitalized = JsonSerializer.Deserialize<DemandLoanResponse>(await capRes.TextAsync(), JsonOptions);
        capitalized!.OutstandingPrincipal.Should().Be(500_164.38m);
        capitalized.RegularAccruedInterest.Should().Be(0m);
        capitalized.Status.Should().Be(nameof(DemandLoanStatus.Restructured));

        // 5. Repay Demand Loan (Partial)
        var repayPayload = new RepayDemandLoanRequest(loanAcc, 100_000m, RepaymentSourceType.Transfer, new DateOnly(2026, 1, 15));
        var repayRes = await api.PostAsync("/api/v1/demand-loans/repay", new() { DataObject = repayPayload });
        repayRes.Status.Should().Be(200);

        var repaid = JsonSerializer.Deserialize<DemandLoanResponse>(await repayRes.TextAsync(), JsonOptions);
        repaid!.OutstandingPrincipal.Should().Be(400_164.38m);

        // 6. Query Loan Status via GET
        var getRes = await api.GetAsync($"/api/v1/demand-loans/{loanAcc}");
        getRes.Status.Should().Be(200);
        var fetched = JsonSerializer.Deserialize<DemandLoanResponse>(await getRes.TextAsync(), JsonOptions);
        fetched!.OutstandingPrincipal.Should().Be(400_164.38m);
    }

    [Fact]
    public async Task Test_ForcedPadFromImportBill_Creation()
    {
        var api = await _fixture.CreateApiContextAsync();

        var forcedPadAcc = AccountNumber.Generate("0101", "451", 20002).Value;
        var forcedPayload = new ForcedPadRequest(
            forcedPadAcc,
            "CIF-IMPORTER-777",
            "0101",
            "LC-IMP-DHAKA-2026-99",
            "BILL-DOC-8888",
            1_500_000m,
            11.5m,
            2.0m,
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 3, 3));

        var res = await api.PostAsync("/api/v1/demand-loans/forced-pad-from-import", new() { DataObject = forcedPayload });
        res.Status.Should().Be(201);

        var pad = JsonSerializer.Deserialize<DemandLoanResponse>(await res.TextAsync(), JsonOptions);
        pad.Should().NotBeNull();
        pad!.ProductCode.Should().Be("451");
        pad.LoanReferenceNumber.Should().Be("PAD-LC-IMP-DHAKA-2026-99-BILL-DOC-8888");
        pad.OutstandingPrincipal.Should().Be(1_500_000m);
        pad.Status.Should().Be(nameof(DemandLoanStatus.Disbursed));
    }

    [Fact]
    public async Task Test_SecurityDepositReceipt_FullLifecycle_IssueLienReleaseRefund()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Issue SDR (BRPD 2.1.4)
        var sdrNumber = "SDR-0101-2026-9001";
        var issuePayload = new IssueSdrRequest(
            sdrNumber,
            "0101",
            100_000m,
            "TENDER-RHD-ROAD-2026-09",
            "Superintending Engineer, RHD Dhaka",
            "CIF-BIDDER-101",
            null,
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 9, 1),
            "TELLER-01",
            "CHECKER-02");

        var issueRes = await api.PostAsync("/api/v1/local-remittance/sdr/issue", new() { DataObject = issuePayload });
        issueRes.Status.Should().Be(201);

        var sdr = JsonSerializer.Deserialize<SdrResponse>(await issueRes.TextAsync(), JsonOptions);
        sdr.Should().NotBeNull();
        sdr!.Commission.Should().Be(100m);
        sdr.VatAmount.Should().Be(15m);
        sdr.TotalPayable.Should().Be(100_115m);
        sdr.Status.Should().Be(nameof(SecurityDepositReceiptStatus.Issued));

        // 2. Mark Lien on SDR
        var lienRes = await api.PostAsync("/api/v1/local-remittance/sdr/lien", new()
        {
            DataObject = new LienSdrRequest(sdrNumber)
        });
        lienRes.Status.Should().Be(200);

        var lienSdr = JsonSerializer.Deserialize<SdrResponse>(await lienRes.TextAsync(), JsonOptions);
        lienSdr!.Status.Should().Be(nameof(SecurityDepositReceiptStatus.LienMarked));

        // 3. Release & Refund SDR to Purchaser
        var releasePayload = new ReleaseSdrRequest(sdrNumber, "RHD-RELEASE-LETTER-441", new DateOnly(2026, 7, 10), "MGR-01");
        var releaseRes = await api.PostAsync("/api/v1/local-remittance/sdr/release-refund", new() { DataObject = releasePayload });
        releaseRes.Status.Should().Be(200);

        var releasedSdr = JsonSerializer.Deserialize<SdrResponse>(await releaseRes.TextAsync(), JsonOptions);
        releasedSdr!.Status.Should().Be(nameof(SecurityDepositReceiptStatus.Refunded));
        releasedSdr.ReleaseLetterReference.Should().Be("RHD-RELEASE-LETTER-441");

        // 4. Query SDR via GET
        var getRes = await api.GetAsync($"/api/v1/local-remittance/sdr/{sdrNumber}");
        getRes.Status.Should().Be(200);
        var fetched = JsonSerializer.Deserialize<SdrResponse>(await getRes.TextAsync(), JsonOptions);
        fetched!.Status.Should().Be(nameof(SecurityDepositReceiptStatus.Refunded));
    }

    [Fact]
    public async Task Test_SecurityDepositReceipt_Stop_Revoke_And_DuplicateWithIndemnity()
    {
        var api = await _fixture.CreateApiContextAsync();

        var sdrNumber = "SDR-0101-2026-9002";
        var issuePayload = new IssueSdrRequest(
            sdrNumber,
            "0101",
            250_000m,
            "TENDER-PWD-CIVIL-2026-44",
            "Chief Engineer, PWD",
            "CIF-BIDDER-202",
            null,
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 10, 1),
            "TELLER-01",
            "CHECKER-02");

        var issueRes = await api.PostAsync("/api/v1/local-remittance/sdr/issue", new() { DataObject = issuePayload });
        issueRes.Status.Should().Be(201);

        // Stop Payment
        var stopRes = await api.PostAsync("/api/v1/local-remittance/sdr/stop-payment", new()
        {
            DataObject = new StopSdrRequest(sdrNumber, "Contractor misplaced instrument", "SUP-01")
        });
        stopRes.Status.Should().Be(200);
        var stoppedSdr = JsonSerializer.Deserialize<SdrResponse>(await stopRes.TextAsync(), JsonOptions);
        stoppedSdr!.Status.Should().Be(nameof(SecurityDepositReceiptStatus.Stopped));

        // Revoke Stop Payment (BRPD 2.1.1-ix, 2.1.2-vi)
        var revokeRes = await api.PostAsync("/api/v1/local-remittance/sdr/revoke-stop-payment", new()
        {
            DataObject = new RevokeStopSdrRequest(sdrNumber, "Instrument found in company safe", "MGR-01")
        });
        revokeRes.Status.Should().Be(200);
        var revokedSdr = JsonSerializer.Deserialize<SdrResponse>(await revokeRes.TextAsync(), JsonOptions);
        revokedSdr!.Status.Should().Be(nameof(SecurityDepositReceiptStatus.Issued));

        // Stop again to test duplicate issuance
        await api.PostAsync("/api/v1/local-remittance/sdr/stop-payment", new()
        {
            DataObject = new StopSdrRequest(sdrNumber, "Confirmed destroyed in fire", "SUP-01")
        });

        // Issue Duplicate backed by indemnity bond (BRPD 2.1.4-ii)
        var dupRes = await api.PostAsync("/api/v1/local-remittance/sdr/issue-duplicate", new()
        {
            DataObject = new IssueDuplicateSdrRequest(sdrNumber, "SDR-0101-2026-9002-DUP", "INDEMNITY-BOND-REG-991122", "MGR-01")
        });
        dupRes.Status.Should().Be(201);
        var dupSdr = JsonSerializer.Deserialize<SdrResponse>(await dupRes.TextAsync(), JsonOptions);
        dupSdr!.SdrNumber.Should().Be("SDR-0101-2026-9002-DUP");
    }
}
