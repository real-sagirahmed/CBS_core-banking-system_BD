// ARCHITECTURAL INTENT: Playwright API Integration tests locking Payment Order (PO) & Demand Draft (DD) Lifecycle,
// Joint-Custody Stock Management, Slab-based Commission/VAT, BFIU Walk-in KYC, and Cryptographic Test Keys
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 1 (1.5.6) & Module 3 (3.11, 3.12).

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CBS.Application.PaymentInstruments.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class PaymentInstrumentPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public PaymentInstrumentPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StockBook_JointCustody_And_Allocation_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var branchCode = $"BR-{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var officerA = "VAULT_OFFICER_01";
        var officerB = "BRANCH_MANAGER_01";

        // 1. Violation: Identical Custodian and Allocator
        var badStockReq = new AllocateStockBookRequest(
            BranchCode: branchCode,
            InstrumentType: PaymentInstrumentType.PayOrder,
            Prefix: "PO",
            StartLeafNumber: 1001,
            TotalLeaves: 50,
            CustodianUserId: officerA,
            AllocatedByUserId: officerA);

        var badRes = await api.PostAsync("/api/v1/payment-instruments/stock/allocate", new() { DataObject = badStockReq });
        badRes.Ok.Should().BeFalse();
        badRes.Status.Should().Be(400);
        (await badRes.TextAsync()).Should().Contain("StockJointCustodianInvariant");

        // 2. Success: Dual Custody allocation
        var validStockReq = new AllocateStockBookRequest(
            BranchCode: branchCode,
            InstrumentType: PaymentInstrumentType.PayOrder,
            Prefix: "PO",
            StartLeafNumber: 1001,
            TotalLeaves: 50,
            CustodianUserId: officerA,
            AllocatedByUserId: officerB);

        var goodRes = await api.PostAsync("/api/v1/payment-instruments/stock/allocate", new() { DataObject = validStockReq });
        goodRes.Ok.Should().BeTrue();
        goodRes.Status.Should().Be(201);

        var stockBook = JsonSerializer.Deserialize<PaymentInstrumentStockBookDto>(await goodRes.TextAsync(), JsonOptions);
        stockBook.Should().NotBeNull();
        stockBook!.BranchCode.Should().Be(branchCode);
        stockBook.TotalLeaves.Should().Be(50);
        stockBook.IssuedLeavesCount.Should().Be(0);
    }

    [Fact]
    public async Task PayOrder_WalkInKyc_And_Encashment_Flow_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var branchCode = $"BR-{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var makerId = "MAKER_01";
        var checkerId = "CHECKER_01";

        // 1. Walk-in above 50,000 without NID must be rejected per BFIU
        var unverifiedWalkInReq = new IssuePayOrderRequest(
            IssuingBranchCode: branchCode,
            Amount: 75_000m,
            PayeeName: "Dhaka Power Distribution Co",
            RemitterName: "Hasan Mahmud",
            RemitterAccountNumber: null,
            IsWalkInCustomer: true,
            WalkInNid: null, // MISSING
            WalkInPhotoId: "DRIVING-LIC-123",
            WalkInMobile: "01700112233",
            TransactionPurpose: "Substation Security Deposit",
            MakerUserId: makerId,
            CheckerUserId: checkerId);

        var badKycRes = await api.PostAsync("/api/v1/payment-instruments/pay-orders/issue", new() { DataObject = unverifiedWalkInReq });
        badKycRes.Ok.Should().BeFalse();
        badKycRes.Status.Should().Be(400);
        (await badKycRes.TextAsync()).Should().Contain("BfiuWalkInKycRequiredInvariant");

        // 2. Verified Walk-in succeeds with slab commission & 15% VAT
        var verifiedWalkInReq = unverifiedWalkInReq with
        {
            WalkInNid = "19882692800045678"
        };

        var issueRes = await api.PostAsync("/api/v1/payment-instruments/pay-orders/issue", new() { DataObject = verifiedWalkInReq });
        var issueText = await issueRes.TextAsync();
        issueRes.Ok.Should().BeTrue(issueText);

        var poDto = JsonSerializer.Deserialize<PaymentInstrumentResponseDto>(issueText, JsonOptions);
        poDto.Should().NotBeNull();
        poDto!.Status.Should().Be("Issued");
        poDto.Amount.Should().Be(75_000m);
        poDto.CommissionAmount.Should().Be(50.00m); // 25K to 100K slab
        poDto.VatAmount.Should().Be(7.50m);        // 15% VAT on 50 BDT
        poDto.TotalDebitedFromCustomer.Should().Be(75057.50m);

        // 3. Counter Encashment
        var encashReq = new EncashPaymentInstrumentRequest(
            PaidByUserId: "TELLER_01",
            PresentedTestKey: null,
            SettlementAccountOrGl: "GL-SUSPENSE-01");

        var encashRes = await api.PostAsync($"/api/v1/payment-instruments/{poDto.InstrumentId}/encash", new() { DataObject = encashReq });
        encashRes.Ok.Should().BeTrue();

        var paidDto = JsonSerializer.Deserialize<PaymentInstrumentResponseDto>(await encashRes.TextAsync(), JsonOptions);
        paidDto!.Status.Should().Be("Paid");
        paidDto.PaidDate.Should().NotBeNull();

        // 4. Double Encashment rejection
        var reEncashRes = await api.PostAsync($"/api/v1/payment-instruments/{poDto.InstrumentId}/encash", new() { DataObject = encashReq });
        reEncashRes.Ok.Should().BeFalse();
        reEncashRes.Status.Should().Be(400);
        (await reEncashRes.TextAsync()).Should().Contain("InstrumentPaymentStatusInvariant");
    }

    [Fact]
    public async Task DemandDraft_CryptographicTestKey_And_DrawingAdvice_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var issuingBranch = $"BR-{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var draweeBranch = $"BR-{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var makerId = "MAKER_01";
        var checkerId = "CHECKER_01";

        // 1. Issue Demand Draft
        var ddIssueReq = new IssueDemandDraftRequest(
            IssuingBranchCode: issuingBranch,
            DraweeBranchCode: draweeBranch,
            Amount: 250_000m,
            PayeeName: "Chittagong Port Authority",
            RemitterName: "Apex Maritime Corp",
            RemitterAccountNumber: "0100987654321",
            IsWalkInCustomer: false,
            WalkInNid: null,
            WalkInPhotoId: null,
            WalkInMobile: null,
            TransactionPurpose: "Container Demurrage Clearing",
            MakerUserId: makerId,
            CheckerUserId: checkerId);

        var ddRes = await api.PostAsync("/api/v1/payment-instruments/demand-drafts/issue", new() { DataObject = ddIssueReq });
        var ddText = await ddRes.TextAsync();
        ddRes.Ok.Should().BeTrue(ddText);

        var ddDto = JsonSerializer.Deserialize<PaymentInstrumentResponseDto>(ddText, JsonOptions);
        ddDto.Should().NotBeNull();
        ddDto!.DdTestKey.Should().NotBeNullOrWhiteSpace();
        ddDto.DdTestKey.Should().StartWith("TK-");
        ddDto.DrawingAdviceTransmitted.Should().BeTrue();

        // 2. Encash at Drawee branch with invalid test key -> FAILS
        var badKeyReq = new EncashPaymentInstrumentRequest(
            PaidByUserId: "DRAWEE_TELLER_01",
            PresentedTestKey: "TK-FORGED99",
            SettlementAccountOrGl: "GL-CLEARING-01");

        var badEncashRes = await api.PostAsync($"/api/v1/payment-instruments/{ddDto.InstrumentId}/encash", new() { DataObject = badKeyReq });
        badEncashRes.Ok.Should().BeFalse();
        badEncashRes.Status.Should().Be(400);
        (await badEncashRes.TextAsync()).Should().Contain("DemandDraftTestKeyMismatchInvariant");

        // 3. Encash at Drawee branch with authentic test key -> SUCCEEDS
        var validKeyReq = new EncashPaymentInstrumentRequest(
            PaidByUserId: "DRAWEE_TELLER_01",
            PresentedTestKey: ddDto.DdTestKey,
            SettlementAccountOrGl: "GL-CLEARING-01");

        var goodEncashRes = await api.PostAsync($"/api/v1/payment-instruments/{ddDto.InstrumentId}/encash", new() { DataObject = validKeyReq });
        goodEncashRes.Ok.Should().BeTrue();

        var paidDd = JsonSerializer.Deserialize<PaymentInstrumentResponseDto>(await goodEncashRes.TextAsync(), JsonOptions);
        paidDd!.Status.Should().Be("Paid");
    }

    [Fact]
    public async Task PaymentOrder_Cancellation_And_DuplicateIssuance_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var branchCode = $"BR-{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var makerId = "MAKER_01";
        var checkerId = "CHECKER_01";

        // 1. Issue PO for cancellation
        var poReq = new IssuePayOrderRequest(
            IssuingBranchCode: branchCode,
            Amount: 30_000m,
            PayeeName: "City Corporation",
            RemitterName: "Tanvir Hasan",
            RemitterAccountNumber: "0100112233445",
            IsWalkInCustomer: false,
            WalkInNid: null,
            WalkInPhotoId: null,
            WalkInMobile: null,
            TransactionPurpose: "Trade License Fee",
            MakerUserId: makerId,
            CheckerUserId: checkerId);

        var issueRes = await api.PostAsync("/api/v1/payment-instruments/pay-orders/issue", new() { DataObject = poReq });
        var issueText = await issueRes.TextAsync();
        issueRes.Ok.Should().BeTrue(issueText);
        var po = JsonSerializer.Deserialize<PaymentInstrumentResponseDto>(issueText, JsonOptions);

        // 2. Cancel instrument with cancellation fee deduction
        var cancelReq = new CancelPaymentInstrumentRequest(
            CancelledByUserId: "OFFICER_02",
            Remarks: "Surrendered by customer as license was cancelled",
            CancellationFee: 100m);

        var cancelRes = await api.PostAsync($"/api/v1/payment-instruments/{po!.InstrumentId}/cancel", new() { DataObject = cancelReq });
        cancelRes.Ok.Should().BeTrue();

        var cancelledPo = JsonSerializer.Deserialize<PaymentInstrumentResponseDto>(await cancelRes.TextAsync(), JsonOptions);
        cancelledPo!.Status.Should().Be("Cancelled");
        cancelledPo.CancellationFeeDeducted.Should().Be(100m);

        // 3. Issue another PO for Duplicate testing
        var po2Req = poReq with { Amount = 120_000m };
        var issue2Res = await api.PostAsync("/api/v1/payment-instruments/pay-orders/issue", new() { DataObject = po2Req });
        var po2 = JsonSerializer.Deserialize<PaymentInstrumentResponseDto>(await issue2Res.TextAsync(), JsonOptions);

        // 4. Duplicate issuance requires Police GD & Indemnity Bond
        var badDupReq = new IssueDuplicateInstrumentRequest(
            OriginalInstrumentId: po2!.InstrumentId,
            PoliceGdNumber: "", // MISSING
            IndemnityBondNumber: "BOND-8877",
            MakerUserId: makerId,
            CheckerUserId: checkerId);

        var badDupRes = await api.PostAsync($"/api/v1/payment-instruments/{po2.InstrumentId}/duplicate", new() { DataObject = badDupReq });
        badDupRes.Ok.Should().BeFalse();
        badDupRes.Status.Should().Be(400);

        // Valid duplicate
        var validDupReq = new IssueDuplicateInstrumentRequest(
            OriginalInstrumentId: po2.InstrumentId,
            PoliceGdNumber: "GD-9988/2026",
            IndemnityBondNumber: "INDEMNITY-BOND-12345",
            MakerUserId: makerId,
            CheckerUserId: checkerId);

        var goodDupRes = await api.PostAsync($"/api/v1/payment-instruments/{po2.InstrumentId}/duplicate", new() { DataObject = validDupReq });
        var goodDupText = await goodDupRes.TextAsync();
        goodDupRes.Ok.Should().BeTrue(goodDupText);
        goodDupRes.Status.Should().Be(201);

        var dupPo = JsonSerializer.Deserialize<PaymentInstrumentResponseDto>(await goodDupRes.TextAsync(), JsonOptions);
        dupPo.Should().NotBeNull();
        dupPo!.Status.Should().Be("Issued");
        dupPo.DuplicateOfInstrumentId.Should().Be(po2.InstrumentId);
        dupPo.PoliceGdNumber.Should().Be("GD-9988/2026");
        dupPo.IndemnityBondNumber.Should().Be("INDEMNITY-BOND-12345");
    }

    [Fact]
    public async Task UnpresentedAgingReport_Returns_Breakdown_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var res = await api.GetAsync($"/api/v1/payment-instruments/reports/unpresented-aging?asOfDate={today:yyyy-MM-dd}");
        res.Ok.Should().BeTrue();

        var report = JsonSerializer.Deserialize<UnpresentedAgingReportDto>(await res.TextAsync(), JsonOptions);
        report.Should().NotBeNull();
        report!.AsOfDate.Should().Be(today);
        report.Items.Should().NotBeNull();
    }
}
