// ARCHITECTURAL INTENT: Playwright API Integration tests locking BACPS Cheque Dishonour Return Advice,
// NI Act Section 138 Legal Notice tracking, Bills for Collection (OBC/IBC), and Inland Bills Purchased (IBP)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 1, 2, and 4, and NI Act 1881.

using System.Text.Json;
using CBS.Application.Clearing.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class ClearingAndBillsPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ClearingAndBillsPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ChequeDishonour_And_Section138_Notice_Lifecycle_Should_Enforce_Statutory_Invariants()
    {
        // ARCHITECTURAL INTENT: End-to-end BACPS Cheque Return Memo, automated charges + 15% VAT,
        // and NI Act 1881 Section 138 legal notice cure window enforcement [BRPD 1.5.3-iii; NI Act 1881 Sec 138]
        var api = await _fixture.CreateApiContextAsync();
        var chequeNo = $"CQ-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var drawerAcc = $"0001-{Guid.NewGuid():N}"[..13];
        var drawerCif = $"CIF-{Guid.NewGuid():N}"[..10];

        // 1. Issue Cheque Dishonour Memo (Regular Value: 85,000 BDT)
        var issueCmd = new IssueChequeDishonourMemoCommand(
            ChequeNumber: chequeNo,
            DrawerAccountNumber: drawerAcc,
            DrawerCif: drawerCif,
            PresenterName: "Square Pharmaceuticals Ltd",
            PresenterBankCode: "SONALIBD",
            PresenterBranchCode: "001",
            ChequeAmount: 85_000m,
            Currency: "BDT",
            DishonourDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ReturnReasonCode: BacpsReturnReasonCode.RR01_InsufficientFunds,
            IssuedByUserId: "CLEARING-OFFICER-01",
            BaseFeeAmount: 200m
        );

        var issueRes = await api.PostAsync("/api/v1/clearing/cheques/dishonour-memo", new APIRequestContextOptions
        {
            DataObject = issueCmd
        });
        issueRes.Status.Should().Be(201);

        var memo = (await issueRes.JsonAsync())?.Deserialize<ChequeDishonourRecordDto>(JsonOptions);
        memo.Should().NotBeNull();
        memo!.ValueType.Should().Be(ClearingValueType.RegularValue);
        memo.DishonourFee.Should().Be(200m);
        memo.VatAmount.Should().Be(30m);
        memo.TotalChargeDebited.Should().Be(230m);
        memo.MemoReferenceNumber.Should().Contain(chequeNo);
        memo.Status.Should().Be(DishonourMemoStatus.Issued);
        memo.NoticeStatus.Should().Be(NiActNoticeStatus.NotIssued);

        // 2. Fetch Formatted Return Advice Print
        var printRes = await api.GetAsync($"/api/v1/clearing/cheques/{memo.Id}/return-memo");
        printRes.Status.Should().Be(200);
        var advicePrint = (await printRes.JsonAsync())?.Deserialize<ChequeReturnAdvicePrintDto>(JsonOptions);
        advicePrint.Should().NotBeNull();
        advicePrint!.StatutoryNotice.Should().Contain("prima facie evidence of dishonour under Section 138");
        advicePrint.ReturnCharges.Should().Be(200m);
        advicePrint.VatCharged.Should().Be(30m);

        // 3. Serve NI Act Section 138 Legal Notice
        var serveCmd = new ServeSection138NoticeCommand(
            DishonourRecordId: memo.Id,
            LegalNoticeReference: $"LN-SEC138-{Guid.NewGuid():N}"[..14],
            NoticeDispatchedDate: DateOnly.FromDateTime(DateTime.UtcNow),
            NoticeServedDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            AdvocateName: "Barrister Mahmudur Rahman"
        );

        var serveRes = await api.PostAsync($"/api/v1/clearing/cheques/{memo.Id}/legal-notice", new APIRequestContextOptions
        {
            DataObject = serveCmd
        });
        serveRes.Status.Should().Be(200);

        var noticeServed = (await serveRes.JsonAsync())?.Deserialize<ChequeDishonourRecordDto>(JsonOptions);
        noticeServed.Should().NotBeNull();
        noticeServed!.NoticeStatus.Should().Be(NiActNoticeStatus.Delivered);
        noticeServed.Status.Should().Be(DishonourMemoStatus.NoticeServed);
        noticeServed.CureDeadlineDate.Should().NotBeNull();

        // 4. Premature Lawsuit filing before 30-day cure window elapses -> Fails with 400
        var prematureSuitCmd = new FileSection138SuitCommand(
            DishonourRecordId: memo.Id,
            SuitCaseNumber: "CR-CASE-2026-9901",
            SuitFilingDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            CourtName: "Chief Metropolitan Magistrate Court, Dhaka"
        );

        var prematureRes = await api.PostAsync($"/api/v1/clearing/cheques/{memo.Id}/lawsuit", new APIRequestContextOptions
        {
            DataObject = prematureSuitCmd
        });
        prematureRes.Status.Should().Be(400); // Section138PrematureSuitFilingInvariant violation

        // 5. Drawer settles payment (Cure) within statutory period
        var cureCmd = new RecordCurePaymentCommand(
            DishonourRecordId: memo.Id,
            PaymentReference: "PO-SETTLED-FULL-01",
            OfficerUserId: "OFFICER-01"
        );

        var cureRes = await api.PostAsync($"/api/v1/clearing/cheques/{memo.Id}/cure", new APIRequestContextOptions
        {
            DataObject = cureCmd
        });
        cureRes.Status.Should().Be(200);

        var cured = (await cureRes.JsonAsync())?.Deserialize<ChequeDishonourRecordDto>(JsonOptions);
        cured.Should().NotBeNull();
        cured!.Status.Should().Be(DishonourMemoStatus.Cured);
        cured.IsSection138SuitEligible.Should().BeFalse();
    }

    [Fact]
    public async Task ChequeDishonour_HighValue_Should_Enforce_Dual_Control_Authorization()
    {
        // ARCHITECTURAL INTENT: High Value clearing (>= 500k BDT) mandates distinct checker authorization [BRPD 1.5.3-vi]
        var api = await _fixture.CreateApiContextAsync();
        var chequeNo = $"CQ-HV-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var drawerAcc = $"0001-{Guid.NewGuid():N}"[..13];
        var drawerCif = $"CIF-{Guid.NewGuid():N}"[..10];

        // 1. High value without checker -> 400 Bad Request
        var missingCheckerCmd = new IssueChequeDishonourMemoCommand(
            ChequeNumber: chequeNo,
            DrawerAccountNumber: drawerAcc,
            DrawerCif: drawerCif,
            PresenterName: "Meghna Group of Industries",
            PresenterBankCode: "ISLAMIBD",
            PresenterBranchCode: "005",
            ChequeAmount: 1_200_000m,
            Currency: "BDT",
            DishonourDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ReturnReasonCode: BacpsReturnReasonCode.RR02_SignatureDiffers,
            IssuedByUserId: "CLEARING-OFFICER-01",
            AuthorizedByCheckerUserId: null // Missing checker
        );

        var missingRes = await api.PostAsync("/api/v1/clearing/cheques/dishonour-memo", new APIRequestContextOptions
        {
            DataObject = missingCheckerCmd
        });
        missingRes.Status.Should().Be(400);

        // 2. High value with valid checker -> 201 Created
        var validHighValCmd = missingCheckerCmd with
        {
            AuthorizedByCheckerUserId = "CLEARING-MANAGER-01"
        };

        var validRes = await api.PostAsync("/api/v1/clearing/cheques/dishonour-memo", new APIRequestContextOptions
        {
            DataObject = validHighValCmd
        });
        validRes.Status.Should().Be(201);

        var highValRecord = (await validRes.JsonAsync())?.Deserialize<ChequeDishonourRecordDto>(JsonOptions);
        highValRecord.Should().NotBeNull();
        highValRecord!.ValueType.Should().Be(ClearingValueType.HighValue);
        highValRecord.AuthorizedByCheckerUserId.Should().Be("CLEARING-MANAGER-01");
    }

    [Fact]
    public async Task BillsForCollection_OBC_And_IBC_Should_Process_Lifecycle_Accurately()
    {
        // ARCHITECTURAL INTENT: Outward & Inward Bills for Collection lifecycle (Lodgement -> Dispatch -> Realization) [BRPD 2.2.4, 2.2.5]
        var api = await _fixture.CreateApiContextAsync();
        var customerAcc = $"0001-{Guid.NewGuid():N}"[..13];
        var customerCif = $"CIF-{Guid.NewGuid():N}"[..10];

        // 1. Lodge OBC (Outward Clean Collection)
        var obcCmd = new LodgeBillForCollectionCommand(
            CollectionType: BillCollectionType.OutwardCleanCollection_OBC,
            CustomerAccountNumber: customerAcc,
            CustomerCif: customerCif,
            DrawerName: "Barisal Fish Exporters",
            DraweeName: "Global Foods Ltd",
            DraweeBankName: "Eastern Bank PLC",
            DraweeBranchName: "Gulshan Branch",
            InstrumentNumber: "DD-881920",
            InstrumentDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            BillAmount: 60_000m,
            Currency: "BDT",
            LodgedDate: DateOnly.FromDateTime(DateTime.UtcNow),
            LodgedByUserId: "TELLER-01"
        );

        var lodgeRes = await api.PostAsync("/api/v1/clearing/bills/obc/lodge", new APIRequestContextOptions
        {
            DataObject = obcCmd
        });
        lodgeRes.Status.Should().Be(201);

        var obc = (await lodgeRes.JsonAsync())?.Deserialize<BillForCollectionDto>(JsonOptions);
        obc.Should().NotBeNull();
        obc!.Status.Should().Be(BillCollectionStatus.Lodged);
        obc.BillNumber.Should().StartWith("OBC-");

        // 2. Dispatch Schedule
        var dispatchCmd = new DispatchBillScheduleCommand(
            BillId: obc.Id,
            ScheduleNumber: "SCHED-2026-OBC-0042",
            ScheduleDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        var dispatchRes = await api.PostAsync($"/api/v1/clearing/bills/{obc.Id}/dispatch", new APIRequestContextOptions
        {
            DataObject = dispatchCmd
        });
        dispatchRes.Status.Should().Be(200);

        // 3. Realize Bill (Commission: 300, Postage: 50 -> Deductions: 350 + 52.50 VAT = 402.50 -> Net: 59,597.50)
        var realizeCmd = new RealizeBillForCollectionCommand(
            BillId: obc.Id,
            CommissionAmount: 300m,
            PostageAmount: 50m,
            RealizationDate: DateOnly.FromDateTime(DateTime.UtcNow),
            CheckerUserId: "CHECKER-01"
        );

        var realizeRes = await api.PostAsync($"/api/v1/clearing/bills/{obc.Id}/realize", new APIRequestContextOptions
        {
            DataObject = realizeCmd
        });
        realizeRes.Status.Should().Be(200);

        var realized = (await realizeRes.JsonAsync())?.Deserialize<BillForCollectionDto>(JsonOptions);
        realized.Should().NotBeNull();
        realized!.Status.Should().Be(BillCollectionStatus.Realized);
        realized.CollectionCommission.Should().Be(300m);
        realized.PostageCharges.Should().Be(50m);
        realized.VatAmount.Should().Be(52.50m);
        realized.NetProceeds.Should().Be(59_597.50m);

        // 4. Lodge IBC (Inward Clean Collection)
        var ibcCmd = new LodgeBillForCollectionCommand(
            CollectionType: BillCollectionType.InwardCleanCollection_IBC,
            CustomerAccountNumber: customerAcc,
            CustomerCif: customerCif,
            DrawerName: "Chittagong Trader",
            DraweeName: "Dhaka Buyer",
            DraweeBankName: "Our Bank PLC",
            DraweeBranchName: "Principal Branch",
            InstrumentNumber: "CQ-INWARD-001",
            InstrumentDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            BillAmount: 25_000m,
            Currency: "BDT",
            LodgedDate: DateOnly.FromDateTime(DateTime.UtcNow),
            LodgedByUserId: "TELLER-01"
        );

        var ibcLodgeRes = await api.PostAsync("/api/v1/clearing/bills/ibc/lodge", new APIRequestContextOptions
        {
            DataObject = ibcCmd
        });
        ibcLodgeRes.Status.Should().Be(201);

        var ibc = (await ibcLodgeRes.JsonAsync())?.Deserialize<BillForCollectionDto>(JsonOptions);
        ibc.Should().NotBeNull();
        ibc!.BillNumber.Should().StartWith("IBC-");

        // 5. Dishonour IBC
        var dishonourCmd = new DishonourBillForCollectionCommand(
            BillId: ibc.Id,
            DishonourReason: "Drawee customer refused acceptance due to goods defect",
            CheckerUserId: "CHECKER-01"
        );

        var dishonourRes = await api.PostAsync($"/api/v1/clearing/bills/{ibc.Id}/dishonour", new APIRequestContextOptions
        {
            DataObject = dishonourCmd
        });
        dishonourRes.Status.Should().Be(200);

        var dishonouredIbc = (await dishonourRes.JsonAsync())?.Deserialize<BillForCollectionDto>(JsonOptions);
        dishonouredIbc.Should().NotBeNull();
        dishonouredIbc!.Status.Should().Be(BillCollectionStatus.ReturnedDishonoured);
    }

    [Fact]
    public async Task InlandBillPurchased_Should_Calculate_Discount_And_Crystallize_Forced_Loan()
    {
        // ARCHITECTURAL INTENT: IBP discounting retains margin, charges interest, and converts default into forced loan [BRPD Module 4]
        var api = await _fixture.CreateApiContextAsync();
        var customerAcc = $"0001-{Guid.NewGuid():N}"[..13];
        var customerCif = $"CIF-{Guid.NewGuid():N}"[..10];

        // 1. Purchase IBP: Face Value = 800,000 BDT, Margin = 15%, Discount = 10%, Tenor = 60 days
        var purchaseCmd = new PurchaseInlandBillCommand(
            CustomerAccountNumber: customerAcc,
            CustomerCif: customerCif,
            DrawerName: "Pran Agro Business Ltd",
            DraweeBankName: "Pubali Bank PLC",
            InstrumentNumber: "BILL-IBP-9922",
            FaceValue: 800_000m,
            Currency: "BDT",
            MarginRatePercent: 15m,
            DiscountRateAnnualPercent: 10m,
            PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            MaturityDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
            PurchasedByOfficerUserId: "OFFICER-01",
            ApprovedByManagerUserId: "MANAGER-CHECKER-01",
            DocumentationFeeAmount: 500m
        );

        var purchaseRes = await api.PostAsync("/api/v1/clearing/ibp/purchase", new APIRequestContextOptions
        {
            DataObject = purchaseCmd
        });
        purchaseRes.Status.Should().Be(201);

        var ibp = (await purchaseRes.JsonAsync())?.Deserialize<InlandBillPurchasedDto>(JsonOptions);
        ibp.Should().NotBeNull();
        ibp!.Status.Should().Be(InlandBillPurchasedStatus.Purchased);
        ibp.MarginRetained.Should().Be(120_000m); // 15% of 800,000
        ibp.DocumentationFee.Should().Be(500m);
        ibp.VatAmount.Should().Be(75m);
        ibp.DisbursedAmount.Should().BeGreaterThan(600_000m);

        // 2. Default at maturity -> Crystallize to Forced Demand Loan
        var forcedLoanAcc = $"FORCED-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
        var crystallizeCmd = new CrystallizeForcedLoanCommand(
            IbpId: ibp.Id,
            ForcedLoanAccountNumber: forcedLoanAcc,
            CrystallizationDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(61)),
            PenaltyRatePercent: 2.0m,
            CheckerUserId: "CREDIT-ADMIN-01"
        );

        var crystallizeRes = await api.PostAsync($"/api/v1/clearing/ibp/{ibp.Id}/crystallize-forced-loan", new APIRequestContextOptions
        {
            DataObject = crystallizeCmd
        });
        crystallizeRes.Status.Should().Be(200);

        var crystallized = (await crystallizeRes.JsonAsync())?.Deserialize<InlandBillPurchasedDto>(JsonOptions);
        crystallized.Should().NotBeNull();
        crystallized!.Status.Should().Be(InlandBillPurchasedStatus.CrystallizedToForcedLoan);
        crystallized.ForcedLoanAccountNumber.Should().Be(forcedLoanAcc);
        // Overdue amount = FaceValue (800,000) - MarginRetained (120,000) = 680,000 BDT
        crystallized.CrystallizedOverdueAmount.Should().Be(680_000m);
        crystallized.PenaltyInterestRatePercent.Should().Be(2.0m);
    }
}
