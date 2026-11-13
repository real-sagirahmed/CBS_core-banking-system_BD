// ARCHITECTURAL INTENT: Playwright API Integration tests locking Back-Value Transaction Approval,
// Retroactive Interest Differentials, Supervisory EOD Audit Journal, and Linked Cascade Reversals
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 1 (1.3.4 & 1.3.6).

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CBS.Application.BackValue.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class BackValueAndCascadeReversalPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public BackValueAndCascadeReversalPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BackValueTransaction_Submit_Approve_And_RetroactiveInterest_Flow_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var makerId = $"MAKER-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var checkerId = $"CHECKER-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var branchCode = $"BR-{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var bookingDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Prohibit Future Value Date
        var futureReq = new SubmitBackValueTransactionRequest(
            TargetType: BackValueTargetType.DepositAccount,
            TargetIdentifier: "0100123456789",
            BranchCode: branchCode,
            BookingDate: bookingDate,
            ValueDate: bookingDate.AddDays(2),
            Amount: 50_000m,
            TransactionNature: DebitCredit.Credit,
            Reason: "Attempting future date",
            RegulatoryReference: "REF-001",
            MakerUserId: makerId);

        var badFutureRes = await api.PostAsync("/api/v1/back-value/requests", new() { DataObject = futureReq });
        badFutureRes.Ok.Should().BeFalse();
        badFutureRes.Status.Should().Be(400);
        (await badFutureRes.TextAsync()).Should().Contain("FutureValueDateProhibitedInvariant");

        // 2. Prohibit Value Date Exceeding 30-Day Window
        var tooOldReq = new SubmitBackValueTransactionRequest(
            TargetType: BackValueTargetType.DepositAccount,
            TargetIdentifier: "0100123456789",
            BranchCode: branchCode,
            BookingDate: bookingDate,
            ValueDate: bookingDate.AddDays(-35),
            Amount: 50_000m,
            TransactionNature: DebitCredit.Credit,
            Reason: "Too old back value",
            RegulatoryReference: "REF-002",
            MakerUserId: makerId);

        var badOldRes = await api.PostAsync("/api/v1/back-value/requests", new() { DataObject = tooOldReq });
        badOldRes.Ok.Should().BeFalse();
        badOldRes.Status.Should().Be(400);
        (await badOldRes.TextAsync()).Should().Contain("BackValueWindowExceededInvariant");

        // 3. Submit Valid Back-Value Request (15 days past, 100,000 BDT)
        var validReq = new SubmitBackValueTransactionRequest(
            TargetType: BackValueTargetType.DepositAccount,
            TargetIdentifier: "0100998877665",
            BranchCode: branchCode,
            BookingDate: bookingDate,
            ValueDate: bookingDate.AddDays(-15),
            Amount: 100_000m,
            TransactionNature: DebitCredit.Credit,
            Reason: "Foreign remittance advice delayed in transit",
            RegulatoryReference: "BRPD-DISP-2026-11",
            MakerUserId: makerId);

        var createRes = await api.PostAsync("/api/v1/back-value/requests", new() { DataObject = validReq });
        createRes.Ok.Should().BeTrue();
        createRes.Status.Should().Be(201);

        var createdDto = JsonSerializer.Deserialize<BackValueTransactionResponseDto>(await createRes.TextAsync(), JsonOptions);
        createdDto.Should().NotBeNull();
        var requestId = createdDto!.RequestId;
        createdDto.Status.Should().Be(BackValueStatus.PendingApproval.ToString());
        createdDto.DaysVariance.Should().Be(15);

        // 4. Prohibit Maker Self-Approval
        var selfApproveReq = new ApproveBackValueTransactionRequest(CheckerUserId: makerId, Remarks: "Self approving");
        var badApproveRes = await api.PostAsync($"/api/v1/back-value/requests/{requestId}/approve", new() { DataObject = selfApproveReq });
        badApproveRes.Ok.Should().BeFalse();
        badApproveRes.Status.Should().Be(400);
        (await badApproveRes.TextAsync()).Should().Contain("BackValueMakerSelfApprovalInvariant");

        // 5. Independent Checker Authorizes Back-Value Posting
        var goodApproveReq = new ApproveBackValueTransactionRequest(CheckerUserId: checkerId, Remarks: "Approved per circular guidelines");
        var goodApproveRes = await api.PostAsync($"/api/v1/back-value/requests/{requestId}/approve", new() { DataObject = goodApproveReq });
        goodApproveRes.Ok.Should().BeTrue();

        var approvedDto = JsonSerializer.Deserialize<BackValueTransactionResponseDto>(await goodApproveRes.TextAsync(), JsonOptions);
        approvedDto.Should().NotBeNull();
        approvedDto!.Status.Should().Be(BackValueStatus.Approved.ToString());
        approvedDto.CheckerUserId.Should().Be(checkerId);
        approvedDto.ExecutedVoucherNumber.Should().NotBeNullOrWhiteSpace();
        approvedDto.RetroactiveInterestCalculated.Should().Be(246.58m); // 100,000 * 6% * (15/365) = 246.58
        approvedDto.RetroactiveInterestVoucherNumber.Should().NotBeNullOrWhiteSpace();

        // 6. Query EOD Supervisory Back-Value Audit Journal
        var journalRes = await api.GetAsync($"/api/v1/back-value/reports/eod-journal?operatingDate={bookingDate:yyyy-MM-dd}&branchCode={branchCode}");
        journalRes.Ok.Should().BeTrue();

        var reportDto = JsonSerializer.Deserialize<EodBackValueJournalReportDto>(await journalRes.TextAsync(), JsonOptions);
        reportDto.Should().NotBeNull();
        reportDto!.TotalBackValueEntriesCount.Should().BeGreaterThanOrEqualTo(1);
        reportDto.TotalPrincipalAmount.Should().BeGreaterThanOrEqualTo(100_000m);
        reportDto.TotalRetroactiveInterestAccrued.Should().BeGreaterThanOrEqualTo(246.58m);
        reportDto.JournalEntries.Should().Contain(e => e.RequestId == requestId);
    }

    [Fact]
    public async Task RetroactiveInterest_Simulate_Differential_Endpoint_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var bookingDate = new DateOnly(2026, 11, 13);
        var valueDate = new DateOnly(2026, 10, 24); // 20 days prior

        var simReq = new SimulateRetroactiveInterestRequest(
            PrincipalAmount: 200_000m,
            AnnualInterestRatePercent: 5.00m,
            ValueDate: valueDate,
            BookingDate: bookingDate,
            TransactionNature: DebitCredit.Debit, // Cash withdrawal clawback
            IsDepositAccount: true);

        var simRes = await api.PostAsync("/api/v1/back-value/interest/simulate-retro", new() { DataObject = simReq });
        simRes.Ok.Should().BeTrue();

        var simDto = JsonSerializer.Deserialize<RetroactiveInterestCalculationResultDto>(await simRes.TextAsync(), JsonOptions);
        simDto.Should().NotBeNull();
        simDto!.DifferentialAmount.Should().Be(547.95m); // 200,000 * 0.05 * (20/365) = 547.95
        simDto.Direction.Should().Be(AdjustmentDirection.DebitCustomer.ToString());
        simDto.DaysPast.Should().Be(20);
        simDto.AccountingNarration.Should().Contain("clawback");
    }

    [Fact]
    public async Task CascadeTransactionReversal_Original_And_LinkedAuxiliary_Flow_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var makerId = $"MAKER-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var checkerId = $"CHECKER-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var voucherNo = $"001-202611-{Guid.NewGuid():N}"[..16].ToUpperInvariant();

        // 1. Prohibit Maker Authorizing Own Reversal
        var badReversalReq = new ReverseTransactionCascadeRequest(
            OriginalVoucherId: $"VCH-{Guid.NewGuid():N}"[..12],
            OriginalVoucherNumber: voucherNo,
            ReversalReason: "Incorrect account debited",
            ReversedByUserId: makerId,
            AuthorizerUserId: makerId, // Self authorizer prohibited
            TotalReversedAmount: 50_150m);

        var badReversalRes = await api.PostAsync("/api/v1/back-value/reversals/cascade", new() { DataObject = badReversalReq });
        badReversalRes.Ok.Should().BeFalse();
        badReversalRes.Status.Should().Be(400);
        (await badReversalRes.TextAsync()).Should().Contain("ReversalMakerCheckerInvariant");

        // 2. Execute Valid Cascade Reversal with Linked Fees and VAT
        var linkedEntries = new List<LinkedAuxiliaryEntryDto>
        {
            new("VCH-FEE-001", ReversalType.LinkedServiceCharge, 100.00m, "Reversal of processing fee"),
            new("VCH-VAT-001", ReversalType.LinkedVat, 15.00m, "Reversal of 15% VAT on fee")
        };

        var goodReversalReq = new ReverseTransactionCascadeRequest(
            OriginalVoucherId: $"VCH-{Guid.NewGuid():N}"[..12],
            OriginalVoucherNumber: voucherNo,
            ReversalReason: "Operational posting error in outward clearing",
            ReversedByUserId: makerId,
            AuthorizerUserId: checkerId,
            TotalReversedAmount: 50_115.00m,
            LinkedAuxiliaryEntries: linkedEntries);

        var goodReversalRes = await api.PostAsync("/api/v1/back-value/reversals/cascade", new() { DataObject = goodReversalReq });
        goodReversalRes.Ok.Should().BeTrue();

        var reversalDto = JsonSerializer.Deserialize<CascadeReversalResponseDto>(await goodReversalRes.TextAsync(), JsonOptions);
        reversalDto.Should().NotBeNull();
        reversalDto!.OriginalVoucherNumber.Should().Be(voucherNo);
        reversalDto.ParentReversalVoucherNumber.Should().Be($"REV-{voucherNo}");
        reversalDto.TotalReversedAmount.Should().Be(50_115.00m);
        reversalDto.LinkedEntriesReversedCount.Should().Be(2);
        reversalDto.IsCascadeComplete.Should().BeTrue();
        reversalDto.ReversedByUserId.Should().Be(makerId);
        reversalDto.AuthorizerUserId.Should().Be(checkerId);
    }
}
