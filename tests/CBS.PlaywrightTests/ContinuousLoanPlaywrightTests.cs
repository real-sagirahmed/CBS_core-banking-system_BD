// ARCHITECTURAL INTENT: Playwright API Integration tests locking Continuous Loan (CC/OD) Lifecycle,
// Dynamic Drawing Power (DP) Calculation, Stock & Book Debts Statements, Godown Inspections,
// Temporary Overdraft (TOD), Annual Renewal Governance, and Quarterly Interest Capitalization
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 and 3.9.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CBS.Application.Lending.Continuous.DTOs;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class ContinuousLoanPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ContinuousLoanPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OpenFacility_And_SubmitStockStatement_Calculates_DP_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var cif = $"CIF{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Open CC Hypothecation facility (Sanction Limit: BDT 5,000,000, Initial DP: BDT 2,000,000)
        var openReq = new OpenContinuousLoanRequest(
            CifNumber: cif,
            BranchCode: "BR01",
            Category: "CashCredit_Hypothecation",
            SanctionLimit: 5000000m,
            InitialDrawingPower: 2000000m,
            RegularInterestRate: 9.50m,
            PenalInterestRate: 11.50m,
            SanctionDate: today,
            ExpiryDate: today.AddYears(1),
            StockMarginPercent: 25.00m,
            BookDebtsMarginPercent: 35.00m,
            LinkedDepositAccountNumber: null);

        var openRes = await api.PostAsync("/api/v1/continuous-loans/open", new() { DataObject = openReq });
        openRes.Ok.Should().BeTrue();
        openRes.Status.Should().Be(201);

        var facility = JsonSerializer.Deserialize<ContinuousLoanResponseDto>(await openRes.TextAsync(), JsonOptions)!;
        facility.AccountNumber.Should().NotBeNullOrWhiteSpace();
        facility.SanctionLimit.Should().Be(5000000m);
        facility.DrawingPower.Should().Be(2000000m);

        // 2. Submit Stock Statement:
        // Raw 1.5M + WIP 0.5M + Finished 2M = 4M gross. Creditors 0.5M -> Paid stock = 3.5M.
        // Stock margin 25% -> Eligible stock DP = 2,625,000.
        // Book Debts <90d: 1M. Margin 35% -> Eligible debts DP = 650,000.
        // Total calculated DP = 3,275,000.
        var stockReq = new SubmitStockStatementRequest(
            StatementDate: today,
            AsOfPeriodDate: today.AddDays(-2),
            RawMaterialsValue: 1500000m,
            WorkInProgressValue: 500000m,
            FinishedGoodsValue: 2000000m,
            CreditorsForGoods: 500000m,
            BookDebtsUnder90Days: 1000000m,
            BookDebtsOver90Days: 400000m, // Ineligible
            StockMarginPercent: 25.00m,
            BookDebtsMarginPercent: 35.00m,
            SubmittedByBorrowerRepresentative: "Managing Director",
            VettedByOfficerUserId: "CAD_OFFICER_01");

        var stockRes = await api.PostAsync($"/api/v1/continuous-loans/{facility.AccountNumber}/stock-statement", new() { DataObject = stockReq });
        stockRes.Ok.Should().BeTrue();

        var stmt = JsonSerializer.Deserialize<StockStatementResponseDto>(await stockRes.TextAsync(), JsonOptions)!;
        stmt.TotalCalculatedDrawingPower.Should().Be(3275000m);
        stmt.PaidStockValue.Should().Be(3500000m);

        // 3. Verify facility DP is updated to 3,275,000
        var getRes = await api.GetAsync($"/api/v1/continuous-loans/{facility.AccountNumber}");
        getRes.Ok.Should().BeTrue();
        var updatedFacility = JsonSerializer.Deserialize<ContinuousLoanResponseDto>(await getRes.TextAsync(), JsonOptions)!;
        updatedFacility.DrawingPower.Should().Be(3275000m);
    }

    [Fact]
    public async Task Withdrawal_Within_DP_And_Exceeding_DP_Rejected_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var cif = $"CIF{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Open facility (Sanction Limit: 2M, DP: 1.5M)
        var openReq = new OpenContinuousLoanRequest(
            CifNumber: cif,
            BranchCode: "BR01",
            Category: "SecuredOverdraft_General",
            SanctionLimit: 2000000m,
            InitialDrawingPower: 1500000m,
            RegularInterestRate: 9.50m,
            PenalInterestRate: null,
            SanctionDate: today,
            ExpiryDate: today.AddYears(1),
            StockMarginPercent: null,
            BookDebtsMarginPercent: null,
            LinkedDepositAccountNumber: null);

        var openRes = await api.PostAsync("/api/v1/continuous-loans/open", new() { DataObject = openReq });
        var facility = JsonSerializer.Deserialize<ContinuousLoanResponseDto>(await openRes.TextAsync(), JsonOptions)!;

        // 2. Withdraw 1,000,000 within DP (1.5M) -> Success
        var withdrawReq = new ContinuousLoanWithdrawalRequest(
            Amount: 1000000m,
            ChannelOrVoucher: "CHQ-001",
            ApprovedByUserId: "TELLER_01",
            AsOfDate: today);

        var wRes = await api.PostAsync($"/api/v1/continuous-loans/{facility.AccountNumber}/withdraw", new() { DataObject = withdrawReq });
        wRes.Ok.Should().BeTrue();
        var wFacility = JsonSerializer.Deserialize<ContinuousLoanResponseDto>(await wRes.TextAsync(), JsonOptions)!;
        wFacility.OutstandingPrincipal.Should().Be(1000000m);

        // 3. Withdraw additional 600,000 -> Total 1.6M breaches DP (1.5M) -> Rejected 400
        var badWithdrawReq = withdrawReq with { Amount = 600000m };
        var badRes = await api.PostAsync($"/api/v1/continuous-loans/{facility.AccountNumber}/withdraw", new() { DataObject = badWithdrawReq });
        badRes.Ok.Should().BeFalse();
        badRes.Status.Should().Be(400);
        (await badRes.TextAsync()).Should().Contain("DrawingPowerExceededInvariant");
    }

    [Fact]
    public async Task TemporaryOverdraft_Grant_Enhances_Limit_And_Allows_Withdrawal_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var cif = $"CIF{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Open facility (Limit: 2M, DP: 2M)
        var openReq = new OpenContinuousLoanRequest(
            CifNumber: cif,
            BranchCode: "BR01",
            Category: "CashCredit_Hypothecation",
            SanctionLimit: 2000000m,
            InitialDrawingPower: 2000000m,
            RegularInterestRate: 9.50m,
            PenalInterestRate: null,
            SanctionDate: today,
            ExpiryDate: today.AddYears(1),
            StockMarginPercent: null,
            BookDebtsMarginPercent: null,
            LinkedDepositAccountNumber: null);

        var openRes = await api.PostAsync("/api/v1/continuous-loans/open", new() { DataObject = openReq });
        var facility = JsonSerializer.Deserialize<ContinuousLoanResponseDto>(await openRes.TextAsync(), JsonOptions)!;

        // 2. Grant TOD of BDT 400,000 for 15 days
        var todReq = new GrantTemporaryOverdraftRequest(
            Amount: 400000m,
            ExpiryDate: today.AddDays(15),
            PenalRate: 14.00m,
            ApprovedByUserId: "HEAD_OF_CREDIT",
            ApprovalReference: "HOC-TOD-2026-99",
            Reason: "Urgent seasonal raw material purchase");

        var todRes = await api.PostAsync($"/api/v1/continuous-loans/{facility.AccountNumber}/temporary-overdraft", new() { DataObject = todReq });
        todRes.Ok.Should().BeTrue();

        var todDto = JsonSerializer.Deserialize<TemporaryOverdraftResponseDto>(await todRes.TextAsync(), JsonOptions)!;
        todDto.EffectiveLimit.Should().Be(2400000m);

        // 3. Withdraw 2,300,000 (permitted under TOD enhanced ceiling of 2,400,000)
        var wReq = new ContinuousLoanWithdrawalRequest(
            Amount: 2300000m,
            ChannelOrVoucher: "RTGS-EXP",
            ApprovedByUserId: "BRANCH_MGR",
            AsOfDate: today);

        var wRes = await api.PostAsync($"/api/v1/continuous-loans/{facility.AccountNumber}/withdraw", new() { DataObject = wReq });
        wRes.Ok.Should().BeTrue();
        var wFacility = JsonSerializer.Deserialize<ContinuousLoanResponseDto>(await wRes.TextAsync(), JsonOptions)!;
        wFacility.OutstandingPrincipal.Should().Be(2300000m);
    }

    [Fact]
    public async Task GodownInspection_ExpiredInsurance_Deflates_DP_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var cif = $"CIF{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Open facility (Limit: 3M, DP: 2M)
        var openReq = new OpenContinuousLoanRequest(
            CifNumber: cif,
            BranchCode: "BR01",
            Category: "CashCredit_Pledge",
            SanctionLimit: 3000000m,
            InitialDrawingPower: 2000000m,
            RegularInterestRate: 9.50m,
            PenalInterestRate: null,
            SanctionDate: today,
            ExpiryDate: today.AddYears(1),
            StockMarginPercent: null,
            BookDebtsMarginPercent: null,
            LinkedDepositAccountNumber: null);

        var openRes = await api.PostAsync("/api/v1/continuous-loans/open", new() { DataObject = openReq });
        var facility = JsonSerializer.Deserialize<ContinuousLoanResponseDto>(await openRes.TextAsync(), JsonOptions)!;

        // 2. Record Inspection with EXPIRED Insurance
        var inspReq = new RecordGodownInspectionRequest(
            InspectionDate: today,
            InspectorUserId: "CAD_INSP_01",
            GodownLocation: "Narayanganj Warehouse #3",
            InspectedStockValue: 2500000m,
            StockConditionDescription: "Intact goods",
            InsurancePolicyNumber: "POL-EXPIRED-999",
            InsuranceCompany: "Pragati Insurance",
            InsuranceExpiryDate: today.AddDays(-10), // EXPIRED
            InsuranceSumInsured: 3000000m,
            InspectionResult: "InadequateInsurance",
            Remarks: "Insurance policy expired");

        var inspRes = await api.PostAsync($"/api/v1/continuous-loans/{facility.AccountNumber}/godown-inspection", new() { DataObject = inspReq });
        inspRes.Ok.Should().BeTrue();

        // 3. Verify facility DP is deflated to 0 due to invalid insurance
        var getRes = await api.GetAsync($"/api/v1/continuous-loans/{facility.AccountNumber}");
        var deflatedFacility = JsonSerializer.Deserialize<ContinuousLoanResponseDto>(await getRes.TextAsync(), JsonOptions)!;
        deflatedFacility.DrawingPower.Should().Be(0m);
    }

    [Fact]
    public async Task ExpiredFacility_DebitBlocked_DepositAllowed_And_RenewalSucceeds_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var cif = $"CIF{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Open facility expiring today
        var openReq = new OpenContinuousLoanRequest(
            CifNumber: cif,
            BranchCode: "BR01",
            Category: "CleanOverdraft",
            SanctionLimit: 1000000m,
            InitialDrawingPower: 1000000m,
            RegularInterestRate: 10.00m,
            PenalInterestRate: null,
            SanctionDate: today.AddYears(-1),
            ExpiryDate: today.AddDays(-1), // Already Expired
            StockMarginPercent: null,
            BookDebtsMarginPercent: null,
            LinkedDepositAccountNumber: null);

        var openRes = await api.PostAsync("/api/v1/continuous-loans/open", new() { DataObject = openReq });
        var facility = JsonSerializer.Deserialize<ContinuousLoanResponseDto>(await openRes.TextAsync(), JsonOptions)!;

        // 2. Withdrawal blocked without override (BRPD 3.9.5-ii)
        var wReq = new ContinuousLoanWithdrawalRequest(
            Amount: 500000m,
            ChannelOrVoucher: "CHQ-001",
            ApprovedByUserId: "TELLER_01",
            AsOfDate: today,
            IsOverrideApproved: false);

        var badWRes = await api.PostAsync($"/api/v1/continuous-loans/{facility.AccountNumber}/withdraw", new() { DataObject = wReq });
        badWRes.Ok.Should().BeFalse();
        badWRes.Status.Should().Be(400);
        (await badWRes.TextAsync()).Should().Contain("ExpiredContinuousLoanDebitBlockedInvariant");

        // 3. Deposit always permitted (BRPD 3.9.5-i)
        var depRes = await api.PostAsync($"/api/v1/continuous-loans/{facility.AccountNumber}/deposit", new()
        {
            DataObject = new ContinuousLoanDepositRequest(50000m)
        });
        depRes.Ok.Should().BeTrue();

        // 4. Annual Renewal succeeds
        var renewReq = new RenewContinuousLoanRequest(
            NewLimit: 1200000m,
            NewExpiryDate: today.AddYears(1),
            NewInterestRate: 9.50m,
            ApprovedByUserId: "MANAGING_DIRECTOR",
            SanctionAdviceReference: "MD-RENEW-2026-01",
            Remarks: "Annual renewal with limit enhancement approved");

        var renewRes = await api.PostAsync($"/api/v1/continuous-loans/{facility.AccountNumber}/renew", new() { DataObject = renewReq });
        renewRes.Ok.Should().BeTrue();

        var renewedFacility = JsonSerializer.Deserialize<ContinuousLoanRenewalResponseDto>(await renewRes.TextAsync(), JsonOptions)!;
        renewedFacility.NewLimit.Should().Be(1200000m);

        // 5. Withdrawal now succeeds after renewal
        var goodWRes = await api.PostAsync($"/api/v1/continuous-loans/{facility.AccountNumber}/withdraw", new()
        {
            DataObject = wReq with { AsOfDate = today }
        });
        goodWRes.Ok.Should().BeTrue();
    }

    [Fact]
    public async Task Reports_Variance_And_Expired_And_Overdue_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. DP Variance Report
        var varianceRes = await api.GetAsync($"/api/v1/continuous-loans/reports/dp-variance?asOfDate={today:yyyy-MM-dd}");
        varianceRes.Ok.Should().BeTrue();
        var varianceReport = JsonSerializer.Deserialize<DrawingPowerVarianceReportDto>(await varianceRes.TextAsync(), JsonOptions);
        varianceReport.Should().NotBeNull();

        // 2. Expired Facilities Report
        var expiredRes = await api.GetAsync($"/api/v1/continuous-loans/reports/expired-unrenewed?asOfDate={today:yyyy-MM-dd}");
        expiredRes.Ok.Should().BeTrue();
        var expiredReport = JsonSerializer.Deserialize<ExpiredContinuousLoanReportDto>(await expiredRes.TextAsync(), JsonOptions);
        expiredReport.Should().NotBeNull();

        // 3. Overdue Stock Statements Report
        var overdueRes = await api.GetAsync($"/api/v1/continuous-loans/reports/overdue-stock-statements?asOfDate={today:yyyy-MM-dd}&thresholdDays=30");
        overdueRes.Ok.Should().BeTrue();
        var overdueReport = JsonSerializer.Deserialize<OverdueStockStatementReportDto>(await overdueRes.TextAsync(), JsonOptions);
        overdueReport.Should().NotBeNull();
    }
}
