// ARCHITECTURAL INTENT: Domain unit tests verifying BACPS Cheque Clearing Dishonour, Return Advice Memos,
// NI Act 1881 Section 138 Statutory Notice & Lawsuit rules, Bills for Collection (OBC/IBC),
// and Inland Bills Purchased (IBP) discounting & forced loan crystallization invariants
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 1, 2, and 4.

using CBS.Domain.Clearing;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class ClearingAndBillsUnitTests
{
    [Fact]
    public void ChequeDishonour_RegularValue_Calculates_Fee_And_Vat_Accurately()
    {
        // ARCHITECTURAL INTENT: Regular Value clearing (< 500,000 BDT) debits standard fee + 15% VAT [BRPD 1.5.3-iii]
        var cheque = new ChequeDishonourRecord(
            id: Guid.NewGuid(),
            chequeNumber: "CQ-8839201",
            drawerAccountNumber: "0001-01002345",
            drawerCif: "CIF-001928",
            presenterName: "Dhaka Trading Syndicate",
            presenterBankCode: "SONALIBD",
            presenterBranchCode: "001",
            chequeAmount: Money.Bdt(45_000m),
            dishonourDate: new DateOnly(2026, 11, 1),
            returnReasonCode: BacpsReturnReasonCode.RR01_InsufficientFunds,
            issuedByUserId: "CLEARING-OFFICER-01",
            baseFeeAmount: 200m
        );

        cheque.ValueType.Should().Be(ClearingValueType.RegularValue);
        cheque.DishonourFee.Amount.Should().Be(200m);
        cheque.VatAmount.Amount.Should().Be(30m); // 15% of 200
        cheque.TotalChargeDebited.Amount.Should().Be(230m);
        cheque.MemoReferenceNumber.Should().StartWith("RET-MEMO-20261101-CQ-8839201");
        cheque.ReturnReasonDescription.Should().Contain("insufficient");
        cheque.Status.Should().Be(DishonourMemoStatus.Issued);
        cheque.NoticeStatus.Should().Be(NiActNoticeStatus.NotIssued);
    }

    [Fact]
    public void ChequeDishonour_HighValue_Requires_Checker_Authorization()
    {
        // ARCHITECTURAL INTENT: High Value clearing (>= 500,000 BDT) mandates dual control [BRPD 1.5.3-vi]
        // 1. Missing checker throws HighValueClearingDualControlInvariant
        var actMissingChecker = () => new ChequeDishonourRecord(
            id: Guid.NewGuid(),
            chequeNumber: "CQ-HV-998811",
            drawerAccountNumber: "0001-01002345",
            drawerCif: "CIF-001928",
            presenterName: "Chittagong Steel Mill",
            presenterBankCode: "AGRANIBD",
            presenterBranchCode: "002",
            chequeAmount: Money.Bdt(750_000m),
            dishonourDate: new DateOnly(2026, 11, 1),
            returnReasonCode: BacpsReturnReasonCode.RR02_SignatureDiffers,
            issuedByUserId: "CLEARING-OFFICER-01",
            authorizedByCheckerUserId: null // Missing checker
        );

        actMissingChecker.Should().Throw<InvariantViolationException>()
            .WithMessage("*High Value*strictly mandates checker authorization*");

        // 2. Maker equals checker throws MakerCheckerSeparationInvariant
        var actSelfApproval = () => new ChequeDishonourRecord(
            id: Guid.NewGuid(),
            chequeNumber: "CQ-HV-998811",
            drawerAccountNumber: "0001-01002345",
            drawerCif: "CIF-001928",
            presenterName: "Chittagong Steel Mill",
            presenterBankCode: "AGRANIBD",
            presenterBranchCode: "002",
            chequeAmount: Money.Bdt(750_000m),
            dishonourDate: new DateOnly(2026, 11, 1),
            returnReasonCode: BacpsReturnReasonCode.RR02_SignatureDiffers,
            issuedByUserId: "CLEARING-OFFICER-01",
            authorizedByCheckerUserId: "CLEARING-OFFICER-01" // Self approval
        );

        actSelfApproval.Should().Throw<InvariantViolationException>()
            .WithMessage("*Maker and Checker user IDs must be strictly distinct*");

        // 3. Valid dual control succeeds
        var validHighValue = new ChequeDishonourRecord(
            id: Guid.NewGuid(),
            chequeNumber: "CQ-HV-998811",
            drawerAccountNumber: "0001-01002345",
            drawerCif: "CIF-001928",
            presenterName: "Chittagong Steel Mill",
            presenterBankCode: "AGRANIBD",
            presenterBranchCode: "002",
            chequeAmount: Money.Bdt(750_000m),
            dishonourDate: new DateOnly(2026, 11, 1),
            returnReasonCode: BacpsReturnReasonCode.RR02_SignatureDiffers,
            issuedByUserId: "CLEARING-OFFICER-01",
            authorizedByCheckerUserId: "MANAGER-CHECKER-01"
        );

        validHighValue.ValueType.Should().Be(ClearingValueType.HighValue);
        validHighValue.AuthorizedByCheckerUserId.Should().Be("MANAGER-CHECKER-01");
    }

    [Fact]
    public void Section138_Notice_Enforces_30_Day_Dispatch_Window()
    {
        // ARCHITECTURAL INTENT: Notice under NI Act Sec 138(b) must be dispatched within 30 days of dishonour memo
        var cheque = new ChequeDishonourRecord(
            id: Guid.NewGuid(),
            chequeNumber: "CQ-771122",
            drawerAccountNumber: "0001-01002345",
            drawerCif: "CIF-001928",
            presenterName: "Padma Agro Ltd",
            presenterBankCode: "JANATABD",
            presenterBranchCode: "005",
            chequeAmount: Money.Bdt(100_000m),
            dishonourDate: new DateOnly(2026, 11, 1),
            returnReasonCode: BacpsReturnReasonCode.RR01_InsufficientFunds,
            issuedByUserId: "CLEARING-OFFICER-01"
        );

        // Dispatched on day 35 (beyond 30 days) -> Throws
        var actLateDispatch = () => cheque.ServeSection138Notice(
            legalNoticeReference: "LN-2026-991",
            noticeDispatchedDate: new DateOnly(2026, 12, 6),
            noticeServedDate: new DateOnly(2026, 12, 8),
            advocateName: "Barrister Rafiqul Islam"
        );

        actLateDispatch.Should().Throw<InvariantViolationException>()
            .WithMessage("*must be dispatched within 30 days*");

        // Dispatched on day 10 -> Valid, sets CureDeadlineDate to NoticeServedDate + 30 days
        cheque.ServeSection138Notice(
            legalNoticeReference: "LN-2026-991",
            noticeDispatchedDate: new DateOnly(2026, 11, 10),
            noticeServedDate: new DateOnly(2026, 11, 12),
            advocateName: "Barrister Rafiqul Islam"
        );

        cheque.NoticeStatus.Should().Be(NiActNoticeStatus.Delivered);
        cheque.Status.Should().Be(DishonourMemoStatus.NoticeServed);
        cheque.CureDeadlineDate.Should().Be(new DateOnly(2026, 12, 12)); // 30 days after served
        cheque.IsSection138SuitEligible.Should().BeFalse();
    }

    [Fact]
    public void Section138_Lawsuit_Enforces_30_Day_Cure_Expiration()
    {
        // ARCHITECTURAL INTENT: Lawsuit under NI Act 138(c) cannot be filed before 30-day cure window expires
        var cheque = new ChequeDishonourRecord(
            id: Guid.NewGuid(),
            chequeNumber: "CQ-554433",
            drawerAccountNumber: "0001-01002345",
            drawerCif: "CIF-001928",
            presenterName: "Meghna Cement Ltd",
            presenterBankCode: "SONALIBD",
            presenterBranchCode: "001",
            chequeAmount: Money.Bdt(200_000m),
            dishonourDate: new DateOnly(2026, 11, 1),
            returnReasonCode: BacpsReturnReasonCode.RR01_InsufficientFunds,
            issuedByUserId: "CLEARING-OFFICER-01"
        );

        cheque.ServeSection138Notice(
            legalNoticeReference: "LN-2026-554",
            noticeDispatchedDate: new DateOnly(2026, 11, 5),
            noticeServedDate: new DateOnly(2026, 11, 7),
            advocateName: "Advocate Kamal Hossain"
        );
        // Cure deadline: 2026-12-07

        // Premature suit filing on 2026-11-20 -> Throws
        var actPrematureSuit = () => cheque.FileSection138Lawsuit(
            suitCaseNumber: "CR-CASE-2026-881",
            suitFilingDate: new DateOnly(2026, 11, 20),
            courtName: "Chief Metropolitan Magistrate Court, Dhaka"
        );

        actPrematureSuit.Should().Throw<InvariantViolationException>()
            .WithMessage("*Cannot file Section 138 lawsuit before the 30-day statutory cure window has elapsed*");

        // Simulate 30 days expiration on 2026-12-08
        cheque.EvaluateNoticeExpiration(new DateOnly(2026, 12, 8));
        cheque.IsSection138SuitEligible.Should().BeTrue();
        cheque.NoticeStatus.Should().Be(NiActNoticeStatus.ThirtyDayWindowExpired);

        // Lawsuit filed on 2026-12-10 -> Valid
        cheque.FileSection138Lawsuit(
            suitCaseNumber: "CR-CASE-2026-881",
            suitFilingDate: new DateOnly(2026, 12, 10),
            courtName: "Chief Metropolitan Magistrate Court, Dhaka"
        );

        cheque.Status.Should().Be(DishonourMemoStatus.LegalSuitFiled);
        cheque.NoticeStatus.Should().Be(NiActNoticeStatus.SuitInstituted);
        cheque.SuitCaseNumber.Should().Be("CR-CASE-2026-881");
    }

    [Fact]
    public void Section138_Cured_Instrument_Prohibits_Suit()
    {
        // ARCHITECTURAL INTENT: Cured cheque cannot be subjected to NI Act Section 138 criminal lawsuit
        var cheque = new ChequeDishonourRecord(
            id: Guid.NewGuid(),
            chequeNumber: "CQ-332211",
            drawerAccountNumber: "0001-01002345",
            drawerCif: "CIF-001928",
            presenterName: "Karnaphuli Paper Ltd",
            presenterBankCode: "RUPALIBD",
            presenterBranchCode: "003",
            chequeAmount: Money.Bdt(50_000m),
            dishonourDate: new DateOnly(2026, 11, 1),
            returnReasonCode: BacpsReturnReasonCode.RR01_InsufficientFunds,
            issuedByUserId: "CLEARING-OFFICER-01"
        );

        cheque.ServeSection138Notice(
            legalNoticeReference: "LN-2026-332",
            noticeDispatchedDate: new DateOnly(2026, 11, 5),
            noticeServedDate: new DateOnly(2026, 11, 7),
            advocateName: "Advocate Aminul Islam"
        );

        // Drawer settles full amount within cure window
        cheque.RecordCurePayment("PO-2026-9901-SETTLED", "OFFICER-01");
        cheque.Status.Should().Be(DishonourMemoStatus.Cured);
        cheque.IsSection138SuitEligible.Should().BeFalse();

        // Attempting to institute lawsuit on cured instrument throws
        var actSuitOnCured = () => cheque.FileSection138Lawsuit("CR-INVALID-01", new DateOnly(2026, 12, 15), "Court");
        actSuitOnCured.Should().Throw<InvariantViolationException>()
            .WithMessage("*Cannot file Section 138 suit for a cured instrument*");
    }

    [Fact]
    public void BillForCollection_OBC_Calculates_Net_Proceeds_With_Commission_Postage_Vat()
    {
        // ARCHITECTURAL INTENT: OBC realization deducts commission, postage, and 15% statutory VAT [BRPD 2.2.4]
        var bill = new BillForCollection(
            id: Guid.NewGuid(),
            billNumber: "OBC-20261101-001",
            collectionType: BillCollectionType.OutwardCleanCollection_OBC,
            customerAccountNumber: "0001-02005544",
            customerCif: "CIF-003322",
            drawerName: "Sylhet Tea Exporters Ltd",
            draweeName: "London Traders Corp",
            draweeBankName: "Standard Chartered Bank, Sylhet",
            draweeBranchName: "Sylhet Main Branch",
            instrumentNumber: "DD-992211",
            instrumentDate: new DateOnly(2026, 10, 25),
            billAmount: Money.Bdt(100_000m),
            lodgedDate: new DateOnly(2026, 11, 1),
            lodgedByUserId: "TELLER-01"
        );

        bill.Status.Should().Be(BillCollectionStatus.Lodged);

        // Dispatch schedule
        bill.DispatchSchedule("SCHED-OBC-2026-001", new DateOnly(2026, 11, 2));
        bill.Status.Should().Be(BillCollectionStatus.Dispatched);

        // Realize bill: Commission = 500 BDT, Postage = 100 BDT -> Total charges = 600 BDT -> VAT 15% = 90 BDT
        // Net Proceeds = 100,000 - 690 = 99,310 BDT
        bill.RealizeBill(
            commissionAmount: 500m,
            postageAmount: 100m,
            realizationDate: new DateOnly(2026, 11, 8),
            checkerUserId: "CHECKER-01"
        );

        bill.Status.Should().Be(BillCollectionStatus.Realized);
        bill.CollectionCommission!.Amount.Should().Be(500m);
        bill.PostageCharges!.Amount.Should().Be(100m);
        bill.VatAmount!.Amount.Should().Be(90m);
        bill.NetProceeds!.Amount.Should().Be(99_310m);
        bill.RealizationDate.Should().Be(new DateOnly(2026, 11, 8));
    }

    [Fact]
    public void BillForCollection_Rejects_Excessive_Deductions_Exceeding_Face_Value()
    {
        // ARCHITECTURAL INTENT: Deductions cannot exceed or equal bill face value [BRPD 2.2.4]
        var bill = new BillForCollection(
            id: Guid.NewGuid(),
            billNumber: "OBC-20261101-002",
            collectionType: BillCollectionType.OutwardCleanCollection_OBC,
            customerAccountNumber: "0001-02005544",
            customerCif: "CIF-003322",
            drawerName: "Micro Merchant",
            draweeName: "Drawee",
            draweeBankName: "Bank",
            draweeBranchName: "Branch",
            instrumentNumber: "CQ-01",
            instrumentDate: new DateOnly(2026, 10, 25),
            billAmount: Money.Bdt(500m),
            lodgedDate: new DateOnly(2026, 11, 1),
            lodgedByUserId: "TELLER-01"
        );

        bill.DispatchSchedule("SCH-01", new DateOnly(2026, 11, 2));

        var actExcessive = () => bill.RealizeBill(400m, 100m, new DateOnly(2026, 11, 5), "CHECKER-01");
        actExcessive.Should().Throw<InvariantViolationException>()
            .WithMessage("*cannot exceed or equal bill face value*");
    }

    [Fact]
    public void InlandBillPurchased_Calculates_Margin_Discount_Interest_And_Net_Disbursed()
    {
        // ARCHITECTURAL INTENT: IBP discounting retains margin, charges interest for tenor, fee, and VAT [BRPD Module 4]
        // Face Value = 1,000,000 BDT, Tenor = 90 days (2026-11-01 to 2026-01-30), Margin = 15%, Discount Rate = 10%
        // Margin = 150,000 BDT
        // Interest = (1,000,000 * 0.10 * 90) / 365 = 24,657.53 BDT
        // Doc Fee = 500 BDT, VAT 15% = 75 BDT
        // Disbursed = 1,000,000 - 150,000 - 24,657.53 - 500 - 75 = 824,767.47 BDT
        var ibp = new InlandBillPurchased(
            id: Guid.NewGuid(),
            ibpReferenceNumber: "IBP-20261101-001",
            customerAccountNumber: "0001-03009988",
            customerCif: "CIF-008877",
            drawerName: "Apex Spinning Mills Ltd",
            draweeBankName: "Islami Bank Bangladesh PLC",
            instrumentNumber: "BILL-2026-7788",
            faceValue: Money.Bdt(1_000_000m),
            marginRatePercent: 15m,
            discountRateAnnualPercent: 10m,
            purchaseDate: new DateOnly(2026, 11, 1),
            maturityDate: new DateOnly(2027, 1, 30),
            purchasedByOfficerUserId: "OFFICER-01",
            approvedByManagerUserId: "MANAGER-CHECKER-01",
            documentationFeeAmount: 500m
        );

        ibp.TenorDays.Should().Be(90);
        ibp.MarginRetained.Amount.Should().Be(150_000m);
        ibp.DiscountInterest.Amount.Should().Be(24_657.53m);
        ibp.DocumentationFee.Amount.Should().Be(500m);
        ibp.VatAmount.Amount.Should().Be(75m);
        ibp.DisbursedAmount.Amount.Should().Be(824_767.47m);
        ibp.Status.Should().Be(InlandBillPurchasedStatus.Purchased);
    }

    [Fact]
    public void InlandBillPurchased_Maker_Checker_Separation()
    {
        // ARCHITECTURAL INTENT: Officer purchasing IBP cannot self-approve as manager [BRPD Module 4]
        var actSelfApproval = () => new InlandBillPurchased(
            id: Guid.NewGuid(),
            ibpReferenceNumber: "IBP-20261101-002",
            customerAccountNumber: "0001-03009988",
            customerCif: "CIF-008877",
            drawerName: "Textile Exporter",
            draweeBankName: "Bank",
            instrumentNumber: "BILL-01",
            faceValue: Money.Bdt(500_000m),
            marginRatePercent: 15m,
            discountRateAnnualPercent: 10m,
            purchaseDate: new DateOnly(2026, 11, 1),
            maturityDate: new DateOnly(2026, 12, 1),
            purchasedByOfficerUserId: "OFFICER-01",
            approvedByManagerUserId: "OFFICER-01" // Self approval
        );

        actSelfApproval.Should().Throw<InvariantViolationException>()
            .WithMessage("*must be strictly distinct*");
    }

    [Fact]
    public void InlandBillPurchased_Crystallizes_To_Forced_Demand_Loan_Upon_Default()
    {
        // ARCHITECTURAL INTENT: Defaulted IBP bill must be converted into forced demand loan [BRPD Module 4]
        var ibp = new InlandBillPurchased(
            id: Guid.NewGuid(),
            ibpReferenceNumber: "IBP-20261101-003",
            customerAccountNumber: "0001-03009988",
            customerCif: "CIF-008877",
            drawerName: "Square Textiles Ltd",
            draweeBankName: "Bank Asia PLC",
            instrumentNumber: "BILL-9901",
            faceValue: Money.Bdt(1_000_000m),
            marginRatePercent: 15m,
            discountRateAnnualPercent: 10m,
            purchaseDate: new DateOnly(2026, 11, 1),
            maturityDate: new DateOnly(2027, 1, 30),
            purchasedByOfficerUserId: "OFFICER-01",
            approvedByManagerUserId: "MANAGER-01"
        );

        // Crystallize to forced demand loan upon maturity default
        // Forced loan amount = FaceValue (1,000,000) - MarginRetained (150,000) = 850,000 BDT
        ibp.CrystallizeForcedLoan(
            forcedLoanAccountNumber: "FORCED-DL-0001-9988",
            crystallizationDate: new DateOnly(2027, 1, 31),
            penaltyRatePercent: 2.0m,
            checkerUserId: "CREDIT-ADMIN-01"
        );

        ibp.Status.Should().Be(InlandBillPurchasedStatus.CrystallizedToForcedLoan);
        ibp.ForcedLoanAccountNumber.Should().Be("FORCED-DL-0001-9988");
        ibp.CrystallizedOverdueAmount!.Amount.Should().Be(850_000m);
        ibp.PenaltyInterestRatePercent.Should().Be(2.0m);
    }
}
