// ARCHITECTURAL INTENT: Playwright API Integration tests locking Wage Earners' 2.5% Remittance Cash Incentive,
// Branch Daily Clean Cash Proofing, and Statutory Reporting Returns
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 11 and FE Circular No. 01/2022.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CBS.Application.Reporting.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class EnterpriseReportingPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public EnterpriseReportingPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RemittanceIncentive_Disburse_SubmitClaim_And_Reimbursement_Flow_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var refNo = $"REMIT-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var acctNo = $"0100{Guid.NewGuid():N}"[..13];
        var cif = $"CIF-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        // 1. Disburse Remittance with 2.5% Cash Incentive
        var disburseReq = new DisburseRemittanceIncentiveRequest(
            RemittanceReferenceNumber: refNo,
            ExchangeHouseCode: "PL-EXCHANGE-DOHA",
            RemitterName: "Jashim Uddin",
            RemitterCountry: "QAT",
            BeneficiaryAccountNumber: acctNo,
            BeneficiaryCif: cif,
            RemittanceAmountBdt: 80_000m,
            DisbursingBranchCode: "BR-001",
            DisbursedByUserId: "TELLER-01");

        var disburseRes = await api.PostAsync("/api/v1/remittance/incentives/disburse", new() { DataObject = disburseReq });
        disburseRes.Ok.Should().BeTrue();
        disburseRes.Status.Should().Be(201);

        var incentiveDoc = JsonSerializer.Deserialize<RemittanceIncentiveResponseDto>(await disburseRes.TextAsync(), JsonOptions);
        incentiveDoc.Should().NotBeNull();
        incentiveDoc!.RemittanceAmountBdt.Should().Be(80_000m);
        incentiveDoc.IncentiveRate.Should().Be(0.025m);
        incentiveDoc.IncentiveAmountBdt.Should().Be(2_000.00m); // 2.5% of 80,000
        incentiveDoc.TotalDisbursedBdt.Should().Be(82_000.00m);
        incentiveDoc.ClaimStatus.Should().Be(RemittanceIncentiveClaimStatus.PendingClaimSubmission.ToString());

        var incentiveId = incentiveDoc.IncentiveId;

        // 2. Query Pending Claims
        var pendingRes = await api.GetAsync("/api/v1/remittance/incentives/pending-claims");
        pendingRes.Ok.Should().BeTrue();
        var pendingList = JsonSerializer.Deserialize<List<RemittanceIncentiveResponseDto>>(await pendingRes.TextAsync(), JsonOptions);
        pendingList.Should().NotBeNull();
        pendingList.Should().Contain(i => i.IncentiveId == incentiveId);

        // 3. Submit Reimbursement Claim to Bangladesh Bank
        var submitClaimReq = new SubmitRemittanceIncentiveBatchClaimRequest(
            IncentiveIds: new List<string> { incentiveId },
            ClaimBatchReference: "BB-CLAIM-QAT-202611",
            SubmitterUserId: "TREASURY-OFFICER-01");

        var submitRes = await api.PostAsync("/api/v1/remittance/incentives/submit-claim", new() { DataObject = submitClaimReq });
        submitRes.Ok.Should().BeTrue();

        var submittedList = JsonSerializer.Deserialize<List<RemittanceIncentiveResponseDto>>(await submitRes.TextAsync(), JsonOptions);
        submittedList.Should().NotBeNull();
        var submittedItem = submittedList!.Find(i => i.IncentiveId == incentiveId);
        submittedItem.Should().NotBeNull();
        submittedItem!.ClaimStatus.Should().Be(RemittanceIncentiveClaimStatus.ClaimSubmittedToCentralBank.ToString());
        submittedItem.ClaimBatchReference.Should().Be("BB-CLAIM-QAT-202611");

        // 4. Record Central Bank Reimbursement
        var reimburseReq = new RecordCentralBankReimbursementRequest(
            CentralBankVoucherReference: "BB-FEOD-CREDIT-778899",
            RecordedByUserId: "TREASURY-HEAD-01");

        var reimburseRes = await api.PostAsync($"/api/v1/remittance/incentives/{incentiveId}/record-reimbursement", new() { DataObject = reimburseReq });
        reimburseRes.Ok.Should().BeTrue();

        var reimbursedDoc = JsonSerializer.Deserialize<RemittanceIncentiveResponseDto>(await reimburseRes.TextAsync(), JsonOptions);
        reimbursedDoc.Should().NotBeNull();
        reimbursedDoc!.ClaimStatus.Should().Be(RemittanceIncentiveClaimStatus.ReimbursedByCentralBank.ToString());
        reimbursedDoc.CentralBankVoucherReference.Should().Be("BB-FEOD-CREDIT-778899");
    }

    [Fact]
    public async Task DailyCleanCash_Generate_And_Certify_Flow_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var branchCode = $"BR-{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var opDate = new DateOnly(2026, 11, 12);

        // 1. Generate Unbalanced Sheet (Physical cash shortage)
        // Opening: 10M, Receipts: 5M, Payments: 3M -> Calculated = 12M. Physical = 11.95M (Shortage 50,000)
        var unbalReq = new GenerateDailyCleanCashRequest(
            BranchCode: branchCode,
            OperatingDate: opDate,
            OpeningVaultCash: 7_000_000m,
            OpeningTellerCash: 3_000_000m,
            TotalCashReceived: 5_000_000m,
            TotalCashPaid: 3_000_000m,
            PhysicalClosingCash: 11_950_000m);

        var unbalRes = await api.PostAsync("/api/v1/reporting/clean-cash/generate", new() { DataObject = unbalReq });
        unbalRes.Ok.Should().BeTrue();
        var unbalDoc = JsonSerializer.Deserialize<DailyCleanCashResponseDto>(await unbalRes.TextAsync(), JsonOptions);
        unbalDoc.Should().NotBeNull();
        var unbalSheetId = unbalDoc!.SheetId;

        // Attempting to certify unbalanced clean cash must return 400 Bad Request
        var certifyReq = new CertifyDailyCleanCashRequest("HEAD-CASHIER-DHAKA", "MANAGER-DHAKA");
        var badCertifyRes = await api.PostAsync($"/api/v1/reporting/clean-cash/{unbalSheetId}/certify", new() { DataObject = certifyReq });
        badCertifyRes.Ok.Should().BeFalse();
        badCertifyRes.Status.Should().Be(400);

        var badErrText = await badCertifyRes.TextAsync();
        badErrText.Should().Contain("DailyCleanCashProofInvariant");

        // 2. Generate Balanced Sheet
        var balancedBranchCode = $"BR-{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var balReq = new GenerateDailyCleanCashRequest(
            BranchCode: balancedBranchCode,
            OperatingDate: opDate,
            OpeningVaultCash: 7_000_000m,
            OpeningTellerCash: 3_000_000m,
            TotalCashReceived: 5_000_000m,
            TotalCashPaid: 3_000_000m,
            PhysicalClosingCash: 12_000_000m); // Exact match

        var balRes = await api.PostAsync("/api/v1/reporting/clean-cash/generate", new() { DataObject = balReq });
        balRes.Ok.Should().BeTrue();
        var balDoc = JsonSerializer.Deserialize<DailyCleanCashResponseDto>(await balRes.TextAsync(), JsonOptions);
        balDoc.Should().NotBeNull();
        var balSheetId = balDoc!.SheetId;

        // 3. Certify Balanced Clean Cash
        var goodCertifyRes = await api.PostAsync($"/api/v1/reporting/clean-cash/{balSheetId}/certify", new() { DataObject = certifyReq });
        goodCertifyRes.Ok.Should().BeTrue();

        var certifiedDoc = JsonSerializer.Deserialize<DailyCleanCashResponseDto>(await goodCertifyRes.TextAsync(), JsonOptions);
        certifiedDoc.Should().NotBeNull();
        certifiedDoc!.ProofStatus.Should().Be(CleanCashProofStatus.BalancedInProof.ToString());
        certifiedDoc.CertifiedByHeadCashierUserId.Should().Be("HEAD-CASHIER-DHAKA");
        certifiedDoc.ApprovedByManagerUserId.Should().Be("MANAGER-DHAKA");
        certifiedDoc.CertifiedAtUtc.Should().NotBeNull();

        // 4. Retrieve by Sheet ID
        var getRes = await api.GetAsync($"/api/v1/reporting/clean-cash/{balSheetId}");
        getRes.Ok.Should().BeTrue();
        var fetchedDoc = JsonSerializer.Deserialize<DailyCleanCashResponseDto>(await getRes.TextAsync(), JsonOptions);
        fetchedDoc.Should().NotBeNull();
        fetchedDoc!.SheetId.Should().Be(balSheetId);
        fetchedDoc.ProofStatus.Should().Be(CleanCashProofStatus.BalancedInProof.ToString());
    }

    [Fact]
    public async Task Statutory_LargeLoan_And_NplPortfolio_Reporting_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Large Loan Exposure Report
        var largeLoanRes = await api.GetAsync("/api/v1/reporting/statutory/large-loans?capitalBase=10000000000");
        largeLoanRes.Ok.Should().BeTrue();

        var largeLoanDoc = JsonSerializer.Deserialize<LargeLoanPortfolioResponseDto>(await largeLoanRes.TextAsync(), JsonOptions);
        largeLoanDoc.Should().NotBeNull();
        largeLoanDoc!.CapitalBaseBdt.Should().Be(10_000_000_000m);
        largeLoanDoc.LargeLoanThresholdBdt.Should().Be(1_000_000_000m); // 10% of 10B

        // 2. NPL Classification Summary Report
        var nplRes = await api.GetAsync("/api/v1/reporting/statutory/npl-portfolio?asOfDate=2026-11-12");
        nplRes.Ok.Should().BeTrue();

        var nplDoc = JsonSerializer.Deserialize<NplClassificationSummaryDto>(await nplRes.TextAsync(), JsonOptions);
        nplDoc.Should().NotBeNull();
        nplDoc!.AsOfDate.Should().Be(new DateOnly(2026, 11, 12));
        nplDoc.TotalLoansCount.Should().BeGreaterThanOrEqualTo(0);
    }
}
