// ARCHITECTURAL INTENT: Playwright API Integration tests locking Telegraphic Transfer (TT) Lifecycle,
// Slab-based Commission & Message Charge, 15% VAT, Cryptographic Test Key Verification, Responding Advice Settlement,
// and Unresponded TT Aging Reporting adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 3 (3.13) and Module 1 (1.6.5).

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CBS.Application.TelegraphicTransfers.DTOs;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class TelegraphicTransferPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public TelegraphicTransferPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IssueTt_Success_And_Calculates_Fees_And_TestKey_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var branchIssuing = $"BR{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var branchDrawee = $"BR{Guid.NewGuid():N}"[..6].ToUpperInvariant();

        // 1. Issue TT for BDT 75,000 (Slab 25K-100K: Comm 60, Msg 30, VAT 13.50)
        var issueReq = new IssueTtRequest(
            IssuingBranchCode: branchIssuing,
            DraweeBranchCode: branchDrawee,
            Amount: 75000.00m,
            PayeeName: "Mizanur Rahman",
            PayeeAccountNumber: "0200889911",
            RemitterName: "Faridul Hasan",
            RemitterAccountNumber: "0100778899",
            TransactionPurpose: "Tuition Fees Remittance",
            SanctionScreeningPassed: true,
            MakerUserId: "TELLER_01",
            CheckerUserId: "MANAGER_01");

        var response = await api.PostAsync("/api/v1/telegraphic-transfers/issue", new() { DataObject = issueReq });
        response.Ok.Should().BeTrue();
        response.Status.Should().Be(201);

        var tt = JsonSerializer.Deserialize<TelegraphicTransferResponseDto>(await response.TextAsync(), JsonOptions);
        tt.Should().NotBeNull();
        tt!.TtId.Should().NotBeNullOrWhiteSpace();
        tt.TtNumber.Should().StartWith($"TT-{branchIssuing}-");
        tt.Amount.Should().Be(75000.00m);
        tt.CommissionAmount.Should().Be(60.00m);
        tt.MessageChargeAmount.Should().Be(30.00m);
        tt.VatAmount.Should().Be(13.50m);
        tt.TotalDebitedFromRemitter.Should().Be(75103.50m);
        tt.TtTestKey.Should().StartWith("TT-TEST-");
        tt.Status.Should().Be("Issued");

        // 2. Fetch by ID
        var getByIdRes = await api.GetAsync($"/api/v1/telegraphic-transfers/{tt.TtId}");
        getByIdRes.Ok.Should().BeTrue();
        var fetchedTt = JsonSerializer.Deserialize<TelegraphicTransferResponseDto>(await getByIdRes.TextAsync(), JsonOptions);
        fetchedTt.Should().NotBeNull();
        fetchedTt!.TtNumber.Should().Be(tt.TtNumber);

        // 3. Fetch by Number
        var getByNumRes = await api.GetAsync($"/api/v1/telegraphic-transfers/by-number/{tt.TtNumber}");
        getByNumRes.Ok.Should().BeTrue();
    }

    [Fact]
    public async Task IssueTt_SanctionsScreening_And_InterBranch_Violations_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var branchA = $"BR{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var branchB = $"BR{Guid.NewGuid():N}"[..6].ToUpperInvariant();

        // 1. Invariant: Sanction Screening Missing
        var sanctionReq = new IssueTtRequest(
            IssuingBranchCode: branchA,
            DraweeBranchCode: branchB,
            Amount: 50000m,
            PayeeName: "Blocked Person",
            PayeeAccountNumber: null,
            RemitterName: "Sender",
            RemitterAccountNumber: null,
            TransactionPurpose: "Suspicious",
            SanctionScreeningPassed: false, // VIOLATION
            MakerUserId: "TELLER_01",
            CheckerUserId: "MANAGER_01");

        var sanctionRes = await api.PostAsync("/api/v1/telegraphic-transfers/issue", new() { DataObject = sanctionReq });
        sanctionRes.Ok.Should().BeFalse();
        sanctionRes.Status.Should().Be(400);
        (await sanctionRes.TextAsync()).Should().Contain("TtSanctionScreeningRequiredInvariant");

        // 2. Invariant: Intra-Branch Transfer Forbidden
        var intraBranchReq = sanctionReq with
        {
            SanctionScreeningPassed = true,
            IssuingBranchCode = branchA,
            DraweeBranchCode = branchA // VIOLATION: Same Branch
        };

        var intraRes = await api.PostAsync("/api/v1/telegraphic-transfers/issue", new() { DataObject = intraBranchReq });
        intraRes.Ok.Should().BeFalse();
        intraRes.Status.Should().Be(400);
        (await intraRes.TextAsync()).Should().Contain("TtInterBranchRequiredInvariant");

        // 3. Invariant: Maker equals Checker Forbidden
        var makerCheckerReq = sanctionReq with
        {
            SanctionScreeningPassed = true,
            MakerUserId = "SAME_USER",
            CheckerUserId = "SAME_USER" // VIOLATION
        };

        var makerCheckerRes = await api.PostAsync("/api/v1/telegraphic-transfers/issue", new() { DataObject = makerCheckerReq });
        makerCheckerRes.Ok.Should().BeFalse();
        makerCheckerRes.Status.Should().Be(400);
        (await makerCheckerRes.TextAsync()).Should().Contain("TtMakerCheckerInvariant");
    }

    [Fact]
    public async Task TransmitAdvice_And_LogTelephonicCallback_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var branchIssuing = $"BR{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var branchDrawee = $"BR{Guid.NewGuid():N}"[..6].ToUpperInvariant();

        // 1. Issue TT
        var issueReq = new IssueTtRequest(
            IssuingBranchCode: branchIssuing,
            DraweeBranchCode: branchDrawee,
            Amount: 120000m,
            PayeeName: "Advisee Person",
            PayeeAccountNumber: "0200112233",
            RemitterName: "Sender",
            RemitterAccountNumber: "0100223344",
            TransactionPurpose: "Commercial Transfer",
            SanctionScreeningPassed: true,
            MakerUserId: "TELLER_01",
            CheckerUserId: "MANAGER_01");

        var issueRes = await api.PostAsync("/api/v1/telegraphic-transfers/issue", new() { DataObject = issueReq });
        var tt = JsonSerializer.Deserialize<TelegraphicTransferResponseDto>(await issueRes.TextAsync(), JsonOptions)!;

        // 2. Transmit Advice
        var transmitReq = new TransmitTtAdviceRequest(TransmittedByUserId: "COMM_OPERATOR_01");
        var transmitRes = await api.PostAsync($"/api/v1/telegraphic-transfers/{tt.TtId}/transmit-advice", new() { DataObject = transmitReq });
        transmitRes.Ok.Should().BeTrue();

        var transmittedTt = JsonSerializer.Deserialize<TelegraphicTransferResponseDto>(await transmitRes.TextAsync(), JsonOptions)!;
        transmittedTt.Status.Should().Be("AdviceTransmitted");
        transmittedTt.AdviceTransmittedByUserId.Should().Be("COMM_OPERATOR_01");
        transmittedTt.AdviceTransmittedAtUtc.Should().NotBeNull();

        // 3. Log Telephonic Callback
        var callbackReq = new LogTelephonicCallbackRequest(
            ReceivingOfficerUserId: "DRAWEE_OFFICER_01",
            VerifiedWithOfficerName: "Mr. Tareq (Issuing Branch)",
            ContactNumber: "+8801912345678",
            Notes: "Verified amount BDT 120,000 and test key calculation.");

        var callbackRes = await api.PostAsync($"/api/v1/telegraphic-transfers/{tt.TtId}/log-telephonic-callback", new() { DataObject = callbackReq });
        callbackRes.Ok.Should().BeTrue();

        var verifiedTt = JsonSerializer.Deserialize<TelegraphicTransferResponseDto>(await callbackRes.TextAsync(), JsonOptions)!;
        verifiedTt.Status.Should().Be("TelephonicallyVerified");
        verifiedTt.TelephonicContactNumber.Should().Be("+8801912345678");
        verifiedTt.TelephonicNotes.Should().Contain("Mr. Tareq (Issuing Branch)");
    }

    [Fact]
    public async Task RespondAndPay_ValidTestKey_SettlesInterBranch_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var branchIssuing = $"BR{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var branchDrawee = $"BR{Guid.NewGuid():N}"[..6].ToUpperInvariant();

        // 1. Issue TT
        var issueReq = new IssueTtRequest(
            IssuingBranchCode: branchIssuing,
            DraweeBranchCode: branchDrawee,
            Amount: 250000m,
            PayeeName: "Settle Payee",
            PayeeAccountNumber: "0200445566",
            RemitterName: "Settle Remitter",
            RemitterAccountNumber: "0100556677",
            TransactionPurpose: "Interbranch settlement",
            SanctionScreeningPassed: true,
            MakerUserId: "TELLER_01",
            CheckerUserId: "MANAGER_01");

        var issueRes = await api.PostAsync("/api/v1/telegraphic-transfers/issue", new() { DataObject = issueReq });
        var tt = JsonSerializer.Deserialize<TelegraphicTransferResponseDto>(await issueRes.TextAsync(), JsonOptions)!;

        // 2. Transmit Advice
        await api.PostAsync($"/api/v1/telegraphic-transfers/{tt.TtId}/transmit-advice", new()
        {
            DataObject = new TransmitTtAdviceRequest("COMM_01")
        });

        // 3. Forged / Wrong Test Key Rejected
        var badRespondReq = new RespondAndPayTtRequest(
            RespondingOfficerUserId: "DRAWEE_MGR_01",
            PresentedTestKey: "TT-TEST-000000", // BAD KEY
            DestinationAccountOrSundryGl: "0200445566");

        var badRespondRes = await api.PostAsync($"/api/v1/telegraphic-transfers/{tt.TtId}/respond-and-pay", new() { DataObject = badRespondReq });
        badRespondRes.Ok.Should().BeFalse();
        badRespondRes.Status.Should().Be(400);
        (await badRespondRes.TextAsync()).Should().Contain("TtTestKeyMismatchInvariant");

        // 4. Authentic Test Key Responding Advice Accepted
        var validRespondReq = badRespondReq with { PresentedTestKey = tt.TtTestKey };
        var validRespondRes = await api.PostAsync($"/api/v1/telegraphic-transfers/{tt.TtId}/respond-and-pay", new() { DataObject = validRespondReq });
        validRespondRes.Ok.Should().BeTrue();

        var settledTt = JsonSerializer.Deserialize<TelegraphicTransferResponseDto>(await validRespondRes.TextAsync(), JsonOptions)!;
        settledTt.Status.Should().Be("Responded");
        settledTt.RespondingAdviceNumber.Should().StartWith($"TT-RESP-{branchDrawee}-");
        settledTt.RespondingOfficerUserId.Should().Be("DRAWEE_MGR_01");
        settledTt.RespondedDate.Should().NotBeNull();

        // 5. Duplicate Responding Prohibited
        var duplicateRes = await api.PostAsync($"/api/v1/telegraphic-transfers/{tt.TtId}/respond-and-pay", new() { DataObject = validRespondReq });
        duplicateRes.Ok.Should().BeFalse();
        duplicateRes.Status.Should().Be(400);
        (await duplicateRes.TextAsync()).Should().Contain("TtAlreadyRespondedInvariant");
    }

    [Fact]
    public async Task CancelTt_BeforeSettlement_And_RejectionAfterSettlement_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var branchIssuing = $"BR{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var branchDrawee = $"BR{Guid.NewGuid():N}"[..6].ToUpperInvariant();

        // 1. Issue & Cancel TT
        var issueReq = new IssueTtRequest(
            IssuingBranchCode: branchIssuing,
            DraweeBranchCode: branchDrawee,
            Amount: 35000m,
            PayeeName: "Cancel Candidate",
            PayeeAccountNumber: null,
            RemitterName: "Remitter",
            RemitterAccountNumber: null,
            TransactionPurpose: "To be cancelled",
            SanctionScreeningPassed: true,
            MakerUserId: "TELLER_01",
            CheckerUserId: "MANAGER_01");

        var issueRes = await api.PostAsync("/api/v1/telegraphic-transfers/issue", new() { DataObject = issueReq });
        var tt = JsonSerializer.Deserialize<TelegraphicTransferResponseDto>(await issueRes.TextAsync(), JsonOptions)!;

        var cancelReq = new CancelTtRequest(
            CancelledByUserId: "BRANCH_MANAGER_01",
            Remarks: "Remitter cancelled transaction before remittance dispatched.");

        var cancelRes = await api.PostAsync($"/api/v1/telegraphic-transfers/{tt.TtId}/cancel", new() { DataObject = cancelReq });
        cancelRes.Ok.Should().BeTrue();

        var cancelledTt = JsonSerializer.Deserialize<TelegraphicTransferResponseDto>(await cancelRes.TextAsync(), JsonOptions)!;
        cancelledTt.Status.Should().Be("Cancelled");
        cancelledTt.CancelledByUserId.Should().Be("BRANCH_MANAGER_01");
        cancelledTt.CancellationRemarks.Should().Contain("Remitter cancelled transaction");

        // 2. Cancellation after settlement is rejected
        var issueReq2 = issueReq with { Amount = 40000m };
        var issueRes2 = await api.PostAsync("/api/v1/telegraphic-transfers/issue", new() { DataObject = issueReq2 });
        var tt2 = JsonSerializer.Deserialize<TelegraphicTransferResponseDto>(await issueRes2.TextAsync(), JsonOptions)!;

        await api.PostAsync($"/api/v1/telegraphic-transfers/{tt2.TtId}/respond-and-pay", new()
        {
            DataObject = new RespondAndPayTtRequest("OFFICER_01", tt2.TtTestKey, "0200888999")
        });

        var cancelSettledRes = await api.PostAsync($"/api/v1/telegraphic-transfers/{tt2.TtId}/cancel", new() { DataObject = cancelReq });
        cancelSettledRes.Ok.Should().BeFalse();
        cancelSettledRes.Status.Should().Be(400);
        (await cancelSettledRes.TextAsync()).Should().Contain("TtCancellationStatusInvariant");
    }

    [Fact]
    public async Task UnrespondedAgingReport_And_OutstandingQueries_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var branchIssuing = $"BR{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var branchDrawee = $"BR{Guid.NewGuid():N}"[..6].ToUpperInvariant();

        // 1. Issue an unresponded TT
        var issueReq = new IssueTtRequest(
            IssuingBranchCode: branchIssuing,
            DraweeBranchCode: branchDrawee,
            Amount: 600000m,
            PayeeName: "Aging Payee",
            PayeeAccountNumber: "0200999888",
            RemitterName: "Aging Remitter",
            RemitterAccountNumber: "0100888777",
            TransactionPurpose: "Aging verification",
            SanctionScreeningPassed: true,
            MakerUserId: "TELLER_01",
            CheckerUserId: "MANAGER_01");

        var issueRes = await api.PostAsync("/api/v1/telegraphic-transfers/issue", new() { DataObject = issueReq });
        issueRes.Ok.Should().BeTrue();

        // 2. Query Outstanding TTs for this branch
        var outstandingRes = await api.GetAsync($"/api/v1/telegraphic-transfers/outstanding?branchCode={branchIssuing}");
        outstandingRes.Ok.Should().BeTrue();
        var outstandingList = JsonSerializer.Deserialize<List<TelegraphicTransferResponseDto>>(await outstandingRes.TextAsync(), JsonOptions)!;
        outstandingList.Should().Contain(t => t.IssuingBranchCode == branchIssuing && t.Amount == 600000m);

        // 3. Query Aging Report
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var agingRes = await api.GetAsync($"/api/v1/telegraphic-transfers/reports/unresponded-aging?asOfDate={today:yyyy-MM-dd}&branchCode={branchIssuing}");
        agingRes.Ok.Should().BeTrue();

        var agingReport = JsonSerializer.Deserialize<UnrespondedTtAgingReportDto>(await agingRes.TextAsync(), JsonOptions)!;
        agingReport.Should().NotBeNull();
        agingReport.TotalUnrespondedCount.Should().BeGreaterOrEqualTo(1);
        agingReport.TotalUnrespondedAmount.Should().BeGreaterOrEqualTo(600000m);
        agingReport.Items.Should().Contain(i => i.IssuingBranchCode == branchIssuing);
    }
}
