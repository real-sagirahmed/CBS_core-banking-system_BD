using System.Text.Json;
using CBS.Application.Specialized.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class SpecializedBankingPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public SpecializedBankingPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Test_AgriculturalCredit_FullLifecycle_InitiateDisburseRepayReschedule()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Initiate Agricultural Loan (BRPD Module 4.4)
        var validFarmerAcc = CBS.Domain.ValueObjects.AccountNumber.Generate("0101", "101", 10001).Value;
        var initiatePayload = new InitiateAgriLoanRequest(
            "CIF-AGRI-1001",
            "KC-BOGURA-88991",
            validFarmerAcc, // Valid 13-digit farmer savings account with check digit
            CropSeasonType.Aman,
            "Khatiyan-55/Daag-12",
            "UP-REF-2026-BOG-01",
            "0101",
            60000m,
            8.0m,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 12, 1));

        var initRes = await api.PostAsync("/api/v1/specialized-lending/agri/initiate", new()
        {
            DataObject = initiatePayload
        });
        initRes.Status.Should().Be(201);

        var loan = JsonSerializer.Deserialize<AgriLoanResponse>(await initRes.TextAsync(), JsonOptions);
        loan.Should().NotBeNull();
        loan!.Status.Should().Be(nameof(AgriculturalLoanStatus.Initiated));
        loan.KrishiCardNumber.Should().Be("KC-BOGURA-88991");

        // 2. Disburse Agricultural Loan
        var disbursePayload = new DisburseAgriLoanRequest(loan.AccountNumber, new DateOnly(2026, 6, 5));
        var disburseRes = await api.PostAsync("/api/v1/specialized-lending/agri/disburse", new()
        {
            DataObject = disbursePayload
        });
        disburseRes.Status.Should().Be(200);

        var disbursed = JsonSerializer.Deserialize<AgriLoanResponse>(await disburseRes.TextAsync(), JsonOptions);
        disbursed!.Status.Should().Be(nameof(AgriculturalLoanStatus.Disbursed));
        disbursed.OutstandingPrincipal.Should().Be(60000m);

        // 3. Process Repayment
        var repayPayload = new RepayAgriLoanRequest(loan.AccountNumber, 10000m);
        var repayRes = await api.PostAsync("/api/v1/specialized-lending/agri/repay", new()
        {
            DataObject = repayPayload
        });
        repayRes.Status.Should().Be(200);

        var repaid = JsonSerializer.Deserialize<AgriLoanResponse>(await repayRes.TextAsync(), JsonOptions);
        repaid!.OutstandingPrincipal.Should().Be(50000m);

        // 4. Natural Disaster Rescheduling (Freeze P&L accrual into non-accrual)
        var reschedulePayload = new RescheduleAgriLoanRequest(
            loan.AccountNumber,
            new DateOnly(2027, 12, 1),
            "MoA-Flood-Gazette-2026-BOG",
            new DateOnly(2026, 10, 1));

        var reschRes = await api.PostAsync("/api/v1/specialized-lending/agri/reschedule-disaster", new()
        {
            DataObject = reschedulePayload
        });
        reschRes.Status.Should().Be(200);

        var rescheduled = JsonSerializer.Deserialize<AgriLoanResponse>(await reschRes.TextAsync(), JsonOptions);
        rescheduled!.IsNonAccrual.Should().BeTrue();
        rescheduled.Status.Should().Be(nameof(AgriculturalLoanStatus.RescheduledNonAccrual));

        // 5. Query Loan Details
        var getRes = await api.GetAsync($"/api/v1/specialized-lending/agri/{loan.AccountNumber}");
        getRes.Status.Should().Be(200);
    }

    [Fact]
    public async Task Test_BillDiscounting_FullLifecycle_DiscountAmortizeLiquidate()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Discount Commercial Bill (BRPD Module 4.6)
        var discountPayload = new DiscountBillRequest(
            "CIF-CORP-4401",
            "IBP-2026-DHAKA-009",
            "125270115",
            "Janata Bank PLC",
            "0101",
            1000000m,
            9.0m,
            90,
            new DateOnly(2026, 5, 1));

        var discRes = await api.PostAsync("/api/v1/specialized-lending/bills/discount", new()
        {
            DataObject = discountPayload
        });
        discRes.Status.Should().Be(201);

        var bill = JsonSerializer.Deserialize<BillDiscountingResponse>(await discRes.TextAsync(), JsonOptions);
        bill.Should().NotBeNull();
        bill!.BillFaceValue.Should().Be(1000000m);
        bill.UpfrontDiscountAmount.Should().Be(22191.78m);
        bill.NetDisbursedAmount.Should().Be(977808.22m);
        bill.UnearnedDiscountBalance.Should().Be(22191.78m);

        // 2. Amortize Unearned Discount to P&L
        var amortizePayload = new AmortizeBillDiscountRequest(bill.AccountNumber, 30);
        var amortRes = await api.PostAsync("/api/v1/specialized-lending/bills/amortize", new()
        {
            DataObject = amortizePayload
        });
        amortRes.Status.Should().Be(200);

        var amortized = JsonSerializer.Deserialize<BillDiscountingResponse>(await amortRes.TextAsync(), JsonOptions);
        amortized!.RecognizedPnLInterest.Should().Be(7397.26m);

        // 3. Liquidate upon Drawee Realization
        var liquidatePayload = new LiquidateBillRequest(bill.AccountNumber, 1000000m);
        var liqRes = await api.PostAsync("/api/v1/specialized-lending/bills/liquidate", new()
        {
            DataObject = liquidatePayload
        });
        liqRes.Status.Should().Be(200);

        var liquidated = JsonSerializer.Deserialize<BillDiscountingResponse>(await liqRes.TextAsync(), JsonOptions);
        liquidated!.Status.Should().Be(nameof(BillDiscountingStatus.RealizedDraweePaid));
        liquidated.UnearnedDiscountBalance.Should().Be(0m);

        // 4. Query Bill Details
        var getRes = await api.GetAsync($"/api/v1/specialized-lending/bills/{bill.AccountNumber}");
        getRes.Status.Should().Be(200);
    }

    [Fact]
    public async Task Test_DepositMultiplication_OpenAndEncash()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Open Double Deposit Multiplication Account (BRPD Module 3.7)
        var openPayload = new OpenMultiplicationAccountRequest(
            "CIF-DEP-5501",
            "0101",
            100000m,
            2, // Double Scheme
            8.0m,
            new DateOnly(2026, 1, 1));

        var openRes = await api.PostAsync("/api/v1/specialized-deposits/multiplication/open", new()
        {
            DataObject = openPayload
        });
        openRes.Status.Should().Be(201);

        var account = JsonSerializer.Deserialize<MultiplicationAccountResponse>(await openRes.TextAsync(), JsonOptions);
        account.Should().NotBeNull();
        account!.MultiplierFactor.Should().Be(2);
        account.CalculatedTenorMonths.Should().Be(105);
        account.MaturityAmount.Should().Be(200000m);

        // 2. Query Account Details
        var getRes = await api.GetAsync($"/api/v1/specialized-deposits/multiplication/{account.AccountNumber}");
        getRes.Status.Should().Be(200);

        // 3. Encash on Maturity
        var encashPayload = new EncashMultiplicationAccountRequest(account.AccountNumber, account.MaturityDate);
        var encashRes = await api.PostAsync("/api/v1/specialized-deposits/multiplication/encash", new()
        {
            DataObject = encashPayload
        });
        encashRes.Status.Should().Be(200);

        var encashed = JsonSerializer.Deserialize<MultiplicationAccountResponse>(await encashRes.TextAsync(), JsonOptions);
        encashed!.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task Test_InterBranchRemittance_IssueAndVerifyPay()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Issue Demand Draft with Cryptographic Test Key (BRPD Module 3.12 & 3.13)
        var issuePayload = new IssueInterBranchInstrumentRequest(
            "DD-2026-CHITTAGONG-001",
            InterBranchInstrumentType.DemandDraft,
            150000m,
            150m,
            22.50m,
            "0101",
            "0202",
            "Rahman Trading Co",
            "Hasan Enterprise",
            new DateOnly(2026, 6, 15));

        var issueRes = await api.PostAsync("/api/v1/inter-branch-remittance/test-key/issue", new()
        {
            DataObject = issuePayload
        });
        issueRes.Status.Should().Be(201);

        var instrument = JsonSerializer.Deserialize<InterBranchInstrumentResponse>(await issueRes.TextAsync(), JsonOptions);
        instrument.Should().NotBeNull();
        instrument!.CryptographicTestKey.Should().HaveLength(5);
        instrument.ContraEntryPosted.Should().BeTrue();
        instrument.Status.Should().Be(nameof(InterBranchInstrumentStatus.IssuedPendingAdvice));

        // 2. Verify and Pay at Destination Branch
        var payPayload = new VerifyAndPayInterBranchInstrumentRequest(
            instrument.InstrumentNumber,
            instrument.CryptographicTestKey,
            "TELLER_CTG_0202",
            new DateOnly(2026, 6, 16));

        var payRes = await api.PostAsync("/api/v1/inter-branch-remittance/test-key/verify-pay", new()
        {
            DataObject = payPayload
        });
        payRes.Status.Should().Be(200);

        var paid = JsonSerializer.Deserialize<InterBranchInstrumentResponse>(await payRes.TextAsync(), JsonOptions);
        paid!.Status.Should().Be(nameof(InterBranchInstrumentStatus.PaidSettled));

        // 3. Query Instrument Details
        var getRes = await api.GetAsync($"/api/v1/inter-branch-remittance/test-key/{instrument.InstrumentNumber}");
        getRes.Status.Should().Be(200);
    }
}
