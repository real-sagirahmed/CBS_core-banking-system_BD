// ARCHITECTURAL INTENT: Playwright API Integration tests locking CBS Transaction Controls Governance,
// PAM Emergency Break-Glass Dual-Custody, and Consolidated Daily EOD Balancing Proofing Engine
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 9, 10 & 11.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CBS.Application.Governance.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class GovernanceAndProofingPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public GovernanceAndProofingPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TransactionAnomaly_And_SelfAccount_Detection_Flow_Should_Enforce_Invariants_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var accountNumber = $"0100{Guid.NewGuid():N}"[..13];
        var cif = $"CIF-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var txnRef = $"TXN-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // 1. Employee self-account posting violation must return 400 Bad Request
        var selfPostingReq = new DetectAnomalyRequest(
            AccountNumber: accountNumber,
            CifNumber: cif,
            TransactionReference: txnRef,
            CurrentAmount: 25_000m,
            Cumulative24hAmount: 25_000m,
            OriginatingTellerId: "EMP-TELLER-77",
            AccountOwnerEmployeeId: "EMP-TELLER-77",
            TransactionTimeUtc: DateTime.UtcNow);

        var selfPostingRes = await api.PostAsync("/api/v1/governance/anomalies/detect", new() { DataObject = selfPostingReq });
        selfPostingRes.Ok.Should().BeFalse();
        selfPostingRes.Status.Should().Be(400);

        var selfErrJson = await selfPostingRes.TextAsync();
        selfErrJson.Should().Contain("EmployeeSelfAccountPostingInvariant");

        // 2. Smurfing / Structuring transaction detection (Cumulative >= 1,000,000 BDT)
        var smurfingReq = new DetectAnomalyRequest(
            AccountNumber: accountNumber,
            CifNumber: cif,
            TransactionReference: $"TXN-SMURF-{Guid.NewGuid():N}"[..14],
            CurrentAmount: 450_000m,
            Cumulative24hAmount: 1_250_000m,
            OriginatingTellerId: "EMP-TELLER-77",
            AccountOwnerEmployeeId: null,
            TransactionTimeUtc: DateTime.UtcNow);

        var smurfRes = await api.PostAsync("/api/v1/governance/anomalies/detect", new() { DataObject = smurfingReq });
        smurfRes.Ok.Should().BeTrue();

        var anomalyDoc = JsonSerializer.Deserialize<TransactionAnomalyResponse>(await smurfRes.TextAsync(), JsonOptions);
        anomalyDoc.Should().NotBeNull();
        anomalyDoc!.AnomalyType.Should().Be(AnomalySuspicionType.SmurfingStructuring.ToString());
        anomalyDoc.Status.Should().Be(AnomalyResolutionStatus.DetectedPendingInvestigation.ToString());

        // 3. Query Pending Anomalies
        var pendingRes = await api.GetAsync("/api/v1/governance/anomalies/pending");
        pendingRes.Ok.Should().BeTrue();
        var pendingList = JsonSerializer.Deserialize<List<TransactionAnomalyResponse>>(await pendingRes.TextAsync(), JsonOptions);
        pendingList.Should().NotBeNull();
        pendingList.Should().Contain(a => a.AnomalyId == anomalyDoc.AnomalyId);

        // 4. Resolve Anomaly
        var resolveReq = new ResolveAnomalyRequest(
            Resolution: AnomalyResolutionStatus.ClearedLegitimateBusiness,
            InvestigatedByUserId: "USR-AML-OFFICER",
            Notes: "Verified bona fide retail cash collection deposit.");

        var resolveRes = await api.PostAsync($"/api/v1/governance/anomalies/{anomalyDoc.AnomalyId}/resolve", new() { DataObject = resolveReq });
        resolveRes.Ok.Should().BeTrue();

        var resolvedDoc = JsonSerializer.Deserialize<TransactionAnomalyResponse>(await resolveRes.TextAsync(), JsonOptions);
        resolvedDoc.Should().NotBeNull();
        resolvedDoc!.Status.Should().Be(AnomalyResolutionStatus.ClearedLegitimateBusiness.ToString());
        resolvedDoc.InvestigatedByUserId.Should().Be("USR-AML-OFFICER");
    }

    [Fact]
    public async Task BreakGlass_Emergency_DualCustody_Session_Flow_Should_Enforce_Invariants_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Request Emergency Break-Glass Session
        var requestorId = $"USR-DBA-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var officer1Id = $"MGR-SEC-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var officer2Id = $"MGR-OPS-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        var reqSession = new RequestBreakGlassSessionRequest(
            RequestorUserId: requestorId,
            TargetRole: "CORE_DBA_SUPERVISOR",
            EmergencyJustification: "Production deadlock triage on trade finance batch tables");

        var createRes = await api.PostAsync("/api/v1/security/break-glass/request", new() { DataObject = reqSession });
        createRes.Ok.Should().BeTrue();

        var sessionDoc = JsonSerializer.Deserialize<BreakGlassSessionResponse>(await createRes.TextAsync(), JsonOptions);
        sessionDoc.Should().NotBeNull();
        var sessionId = sessionDoc!.SessionId;
        sessionDoc.Status.Should().Be(BreakGlassStatus.RequestedPendingApproval.ToString());

        // 2. Self-Approval by requestor must fail (Dual custody violation)
        var selfApproveReq = new ApproveBreakGlassSessionRequest(requestorId);
        var selfApproveRes = await api.PostAsync($"/api/v1/security/break-glass/{sessionId}/approve", new() { DataObject = selfApproveReq });
        selfApproveRes.Ok.Should().BeFalse();
        selfApproveRes.Status.Should().Be(400);

        // 3. First Officer Approval succeeds
        var officer1Req = new ApproveBreakGlassSessionRequest(officer1Id);
        var officer1Res = await api.PostAsync($"/api/v1/security/break-glass/{sessionId}/approve", new() { DataObject = officer1Req });
        officer1Res.Ok.Should().BeTrue();

        // 4. Same officer approving second time must fail
        var sameOfficerRes = await api.PostAsync($"/api/v1/security/break-glass/{sessionId}/approve", new() { DataObject = officer1Req });
        sameOfficerRes.Ok.Should().BeFalse();
        sameOfficerRes.Status.Should().Be(400);

        // 5. Second Distinct Officer Approval activates 4-hour window
        var officer2Req = new ApproveBreakGlassSessionRequest(officer2Id);
        var officer2Res = await api.PostAsync($"/api/v1/security/break-glass/{sessionId}/approve", new() { DataObject = officer2Req });
        officer2Res.Ok.Should().BeTrue();

        var activeDoc = JsonSerializer.Deserialize<BreakGlassSessionResponse>(await officer2Res.TextAsync(), JsonOptions);
        activeDoc.Should().NotBeNull();
        activeDoc!.Status.Should().Be(BreakGlassStatus.ApprovedActive.ToString());
        activeDoc.SessionExpiresAtUtc.Should().NotBeNull();

        // 6. Validate Active Session
        var validateRes = await api.PostAsync($"/api/v1/security/break-glass/{sessionId}/validate", new() { });
        validateRes.Ok.Should().BeTrue();

        // 7. Revoke Session
        var revokeReq = new RevokeBreakGlassSessionRequest("USR-AUDITOR", "Production issue resolved.");
        var revokeRes = await api.PostAsync($"/api/v1/security/break-glass/{sessionId}/revoke", new() { DataObject = revokeReq });
        revokeRes.Ok.Should().BeTrue();

        // 8. Validating Revoked Session must fail
        var postRevokeRes = await api.PostAsync($"/api/v1/security/break-glass/{sessionId}/validate", new() { });
        postRevokeRes.Ok.Should().BeFalse();
        postRevokeRes.Status.Should().Be(400);
    }

    [Fact]
    public async Task Consolidated_Daily_Eod_Balancing_Proofing_Flow_Should_Enforce_Invariants_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var branchCode = $"BR-{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var opDate = new DateOnly(2026, 11, 12);

        // 1. Generate Unbalanced Sheet (GL Debits != GL Credits)
        var unbalReq = new GenerateDailyBalancingSheetRequest(
            BranchCode: branchCode,
            OperatingDate: opDate,
            TotalGlDebits: 100_000_000m,
            TotalGlCredits: 99_500_000m, // 500,000 difference
            TotalVaultCashBalance: 20_000_000m,
            TotalTellerCashBalance: 5_000_000m,
            TotalClearingSettlementDebits: 15_000_000m,
            TotalClearingSettlementCredits: 15_000_000m,
            ClearingSuspenseBalance: 0m);

        var unbalRes = await api.PostAsync("/api/v1/eod/balancing/generate", new() { DataObject = unbalReq });
        unbalRes.Ok.Should().BeTrue();

        var unbalDoc = JsonSerializer.Deserialize<DailyEodBalancingSheetResponse>(await unbalRes.TextAsync(), JsonOptions);
        unbalDoc.Should().NotBeNull();
        var unbalSheetId = unbalDoc!.SheetId;

        // Attempting to proof unbalanced sheet must throw InvariantViolationException (400)
        var proofReq = new ProofDailyBooksRequest("MGR-BRANCH-HEAD");
        var badProofRes = await api.PostAsync($"/api/v1/eod/balancing/{unbalSheetId}/verify-and-proof", new() { DataObject = proofReq });
        badProofRes.Ok.Should().BeFalse();
        badProofRes.Status.Should().Be(400);

        var badProofErr = await badProofRes.TextAsync();
        badProofErr.Should().Contain("DailyEodBalancingProofInvariant");

        // 2. Generate Balanced Sheet
        var balancedBranchCode = $"BR-{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        var balReq = new GenerateDailyBalancingSheetRequest(
            BranchCode: balancedBranchCode,
            OperatingDate: opDate,
            TotalGlDebits: 250_000_000m,
            TotalGlCredits: 250_000_000m,
            TotalVaultCashBalance: 50_000_000m,
            TotalTellerCashBalance: 12_000_000m,
            TotalClearingSettlementDebits: 35_000_000m,
            TotalClearingSettlementCredits: 35_000_000m,
            ClearingSuspenseBalance: 0m);

        var balRes = await api.PostAsync("/api/v1/eod/balancing/generate", new() { DataObject = balReq });
        balRes.Ok.Should().BeTrue();

        var balDoc = JsonSerializer.Deserialize<DailyEodBalancingSheetResponse>(await balRes.TextAsync(), JsonOptions);
        balDoc.Should().NotBeNull();
        var balSheetId = balDoc!.SheetId;

        // 3. Proof Balanced Books succeeds
        var goodProofRes = await api.PostAsync($"/api/v1/eod/balancing/{balSheetId}/verify-and-proof", new() { DataObject = proofReq });
        goodProofRes.Ok.Should().BeTrue();

        var proofedDoc = JsonSerializer.Deserialize<DailyEodBalancingSheetResponse>(await goodProofRes.TextAsync(), JsonOptions);
        proofedDoc.Should().NotBeNull();
        proofedDoc!.ProofingStatus.Should().Be(EodProofingStatus.BalancedInProof.ToString());
        proofedDoc.ApprovedByManagerUserId.Should().Be("MGR-BRANCH-HEAD");
        proofedDoc.ProofedAtUtc.Should().NotBeNull();

        // 4. Retrieve by Sheet ID
        var getRes = await api.GetAsync($"/api/v1/eod/balancing/{balSheetId}");
        getRes.Ok.Should().BeTrue();
        var fetchedDoc = JsonSerializer.Deserialize<DailyEodBalancingSheetResponse>(await getRes.TextAsync(), JsonOptions);
        fetchedDoc.Should().NotBeNull();
        fetchedDoc!.SheetId.Should().Be(balSheetId);
        fetchedDoc.ProofingStatus.Should().Be(EodProofingStatus.BalancedInProof.ToString());
    }
}
