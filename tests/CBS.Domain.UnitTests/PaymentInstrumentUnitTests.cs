// ARCHITECTURAL INTENT: Domain Unit Tests for Payment Order (PO) & Demand Draft (DD) Lifecycle,
// Joint-Custody Stock Management, Slab-based Commission/VAT, BFIU Walk-in KYC, and Cryptographic Test Keys
// adhering to Bangladesh Bank BRPD Circular No. 12 (1.5.6, 3.11, 3.12).

using System;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.PaymentInstruments;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class PaymentInstrumentUnitTests
{
    private readonly DateOnly _today = new(2026, 11, 13);

    // =========================================================================
    // 1. PHYSICAL LEAF STOCK MANAGEMENT & JOINT CUSTODY TESTS (BRPD 1.5.6-ii, 3.11.1-iv)
    // =========================================================================

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public void StockBook_ValidDenomination_InitializesCorrectly(int leaves)
    {
        var book = new PaymentInstrumentStockBook(
            bookId: "STK-PO-01",
            branchCode: "BR01",
            instrumentType: PaymentInstrumentType.PayOrder,
            prefix: "PO-DHK",
            startLeafNumber: 1001,
            totalLeaves: leaves,
            custodianUserId: "VAULT_OFFICER",
            allocatedByUserId: "BRANCH_MANAGER");

        book.EndLeafNumber.Should().Be(1000 + leaves);
        book.TotalLeaves.Should().Be(leaves);
        book.IssuedLeavesCount.Should().Be(0);
        book.NextAvailableLeafNumber.Should().Be(1001);
        book.IsExhausted.Should().BeFalse();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(200)]
    public void StockBook_InvalidDenomination_ThrowsInvariantViolationException(int invalidLeaves)
    {
        Action act = () => new PaymentInstrumentStockBook(
            bookId: "STK-PO-BAD",
            branchCode: "BR01",
            instrumentType: PaymentInstrumentType.PayOrder,
            prefix: "PO-DHK",
            startLeafNumber: 1,
            totalLeaves: invalidLeaves,
            custodianUserId: "OFFICER1",
            allocatedByUserId: "OFFICER2");

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("StockBookDenominationInvariant");
        ex.RegulatoryReference.Should().Contain("1.5.6-ii");
    }

    [Fact]
    public void StockBook_JointCustodianViolation_ThrowsInvariantViolationException()
    {
        Action act = () => new PaymentInstrumentStockBook(
            bookId: "STK-PO-SELF",
            branchCode: "BR01",
            instrumentType: PaymentInstrumentType.PayOrder,
            prefix: "PO-DHK",
            startLeafNumber: 1,
            totalLeaves: 25,
            custodianUserId: "SAME_USER",
            allocatedByUserId: "SAME_USER");

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("StockJointCustodianInvariant");
        ex.RegulatoryReference.Should().Contain("3.11.1-iv");
    }

    [Fact]
    public void StockBook_AllocateLeaf_AdvancesCounter_AndExhaustsAtLimit()
    {
        var book = new PaymentInstrumentStockBook(
            bookId: "STK-PO-EXH",
            branchCode: "BR01",
            instrumentType: PaymentInstrumentType.PayOrder,
            prefix: "PO",
            startLeafNumber: 501,
            totalLeaves: 25,
            custodianUserId: "OFFICER_A",
            allocatedByUserId: "OFFICER_B");

        var firstLeaf = book.AllocateNextLeaf();
        firstLeaf.Should().Be("PO-0000501");
        book.IssuedLeavesCount.Should().Be(1);
        book.NextAvailableLeafNumber.Should().Be(502);

        // Exhaust remaining 24 leaves
        for (int i = 0; i < 24; i++)
        {
            book.AllocateNextLeaf();
        }

        book.IssuedLeavesCount.Should().Be(25);
        book.IsExhausted.Should().BeTrue();

        Action overDraw = () => book.AllocateNextLeaf();
        var ex = overDraw.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("StockBookExhaustedInvariant");
        ex.RegulatoryReference.Should().Contain("1.5.6-ii");
    }

    // =========================================================================
    // 2. SLAB-BASED COMMISSION & 15% VAT CALCULATOR TESTS (BRPD 1.5.6-iv, 3.11.1-i, 3.12.1-i)
    // =========================================================================

    [Theory]
    [InlineData(500, 25.00, 3.75, InstrumentSlabType.UpTo25K)]
    [InlineData(50_000, 50.00, 7.50, InstrumentSlabType.From25KTo100K)]
    [InlineData(250_000, 100.00, 15.00, InstrumentSlabType.From100KTo500K)]
    [InlineData(750_000, 200.00, 30.00, InstrumentSlabType.From500KTo1M)]
    [InlineData(2_000_000, 300.00, 45.00, InstrumentSlabType.Above1M)]
    public void PaymentOrder_CommissionAndVat_CalculatesAccurately(decimal amount, decimal expectedComm, decimal expectedVat, InstrumentSlabType expectedSlab)
    {
        var (comm, vat, slab) = InstrumentCommissionCalculator.Calculate(PaymentInstrumentType.PayOrder, amount);

        comm.Should().Be(expectedComm);
        vat.Should().Be(expectedVat);
        slab.Should().Be(expectedSlab);
    }

    [Theory]
    [InlineData(10_000, 30.00, 4.50, InstrumentSlabType.UpTo25K)]
    [InlineData(50_000, 60.00, 9.00, InstrumentSlabType.From25KTo100K)]
    [InlineData(500_000, 120.00, 18.00, InstrumentSlabType.From100KTo500K)]
    [InlineData(1_000_000, 250.00, 37.50, InstrumentSlabType.From500KTo1M)]
    [InlineData(2_000_000, 350.00, 52.50, InstrumentSlabType.Above1M)]
    public void DemandDraft_CommissionAndVat_CalculatesAccurately(decimal amount, decimal expectedComm, decimal expectedVat, InstrumentSlabType expectedSlab)
    {
        var (comm, vat, slab) = InstrumentCommissionCalculator.Calculate(PaymentInstrumentType.DemandDraft, amount);

        comm.Should().Be(expectedComm);
        vat.Should().Be(expectedVat);
        slab.Should().Be(expectedSlab);
    }

    // =========================================================================
    // 3. BFIU WALK-IN KYC & SANCTION INVARIANTS (BRPD 1.5.6-vi, 3.11.1-v)
    // =========================================================================

    [Fact]
    public void PaymentInstrument_WalkInCustomerAbove50k_MissingNid_ThrowsInvariantViolationException()
    {
        Action act = () => new PaymentInstrument(
            instrumentId: "INST-001",
            instrumentType: PaymentInstrumentType.PayOrder,
            instrumentNumber: "PO-0001001",
            issuingBranchCode: "BR01",
            draweeBranchCode: null,
            amount: Money.Bdt(50_000m),
            payeeName: "ABC Supplies Ltd",
            remitterName: "Rahim Uddin",
            remitterAccountNumber: null,
            isWalkInCustomer: true,
            walkInNid: null, // MISSING NID
            walkInPhotoId: "PASSPORT-123",
            walkInMobile: "01711223344",
            transactionPurpose: "Tender Security Deposit",
            issueDate: _today,
            issuedByUserId: "MAKER_01",
            authorizedByUserId: "CHECKER_01");

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("BfiuWalkInKycRequiredInvariant");
        ex.RegulatoryReference.Should().Contain("1.5.6-vi");
    }

    [Fact]
    public void PaymentInstrument_WalkInCustomerAbove50k_WithVerifiedNidAndMobile_Succeeds()
    {
        var inst = new PaymentInstrument(
            instrumentId: "INST-002",
            instrumentType: PaymentInstrumentType.PayOrder,
            instrumentNumber: "PO-0001002",
            issuingBranchCode: "BR01",
            draweeBranchCode: null,
            amount: Money.Bdt(75_000m),
            payeeName: "Dhaka University",
            remitterName: "Farid Ahmed",
            remitterAccountNumber: null,
            isWalkInCustomer: true,
            walkInNid: "19902692800012345",
            walkInPhotoId: "NID-ORIGINAL",
            walkInMobile: "01811223344",
            transactionPurpose: "Admission Fees",
            issueDate: _today,
            issuedByUserId: "MAKER_01",
            authorizedByUserId: "CHECKER_01");

        inst.Status.Should().Be(PaymentInstrumentStatus.Issued);
        inst.CommissionAmount.Should().Be(50m);
        inst.VatAmount.Should().Be(7.50m);
    }

    [Fact]
    public void PaymentInstrument_AccountHolderCustomerAbove50k_WithoutWalkInNid_Succeeds()
    {
        var inst = new PaymentInstrument(
            instrumentId: "INST-003",
            instrumentType: PaymentInstrumentType.PayOrder,
            instrumentNumber: "PO-0001003",
            issuingBranchCode: "BR01",
            draweeBranchCode: null,
            amount: Money.Bdt(500_000m),
            payeeName: "Customs Authority",
            remitterName: "Global Trade Corp",
            remitterAccountNumber: "0100123456789",
            isWalkInCustomer: false,
            walkInNid: null,
            walkInPhotoId: null,
            walkInMobile: null,
            transactionPurpose: "Duty Payment",
            issueDate: _today,
            issuedByUserId: "MAKER_01",
            authorizedByUserId: "CHECKER_01");

        inst.Status.Should().Be(PaymentInstrumentStatus.Issued);
        inst.CommissionAmount.Should().Be(100m);
        inst.VatAmount.Should().Be(15.00m);
    }

    [Fact]
    public void PaymentInstrument_MakerCheckerSame_ThrowsInvariantViolationException()
    {
        Action act = () => new PaymentInstrument(
            instrumentId: "INST-004",
            instrumentType: PaymentInstrumentType.PayOrder,
            instrumentNumber: "PO-0001004",
            issuingBranchCode: "BR01",
            draweeBranchCode: null,
            amount: Money.Bdt(10_000m),
            payeeName: "Water Supply Authority",
            remitterName: "Kamal Hossain",
            remitterAccountNumber: "0100123456789",
            isWalkInCustomer: false,
            walkInNid: null,
            walkInPhotoId: null,
            walkInMobile: null,
            transactionPurpose: "Utility Bill",
            issueDate: _today,
            issuedByUserId: "SAME_OFFICER",
            authorizedByUserId: "SAME_OFFICER");

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("InstrumentMakerCheckerInvariant");
        ex.RegulatoryReference.Should().Contain("1.3.18");
    }

    // =========================================================================
    // 4. DEMAND DRAFT TEST KEY & DRAWEE ADVICE TESTS (BRPD 3.12.1-iii, 3.12.2-i)
    // =========================================================================

    [Fact]
    public void DemandDraft_RequiresDraweeBranch_ThrowsWhenMissing()
    {
        Action act = () => new PaymentInstrument(
            instrumentId: "DD-001",
            instrumentType: PaymentInstrumentType.DemandDraft,
            instrumentNumber: "DD-0005001",
            issuingBranchCode: "BR-DHAKA",
            draweeBranchCode: null, // MISSING
            amount: Money.Bdt(100_000m),
            payeeName: "Chittagong Port Authority",
            remitterName: "Logistics Ltd",
            remitterAccountNumber: "0100123456789",
            isWalkInCustomer: false,
            walkInNid: null,
            walkInPhotoId: null,
            walkInMobile: null,
            transactionPurpose: "Port Dues",
            issueDate: _today,
            issuedByUserId: "MAKER_01",
            authorizedByUserId: "CHECKER_01");

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("DemandDraftDraweeBranchRequiredInvariant");
        ex.RegulatoryReference.Should().Contain("3.12.1-i");
    }

    [Fact]
    public void DemandDraft_TestKeyGenerationAndVerification_OperatesCorrectly()
    {
        var testKey = DdTestKeyCalculator.GenerateTestKey(
            issueDate: _today,
            issuingBranchCode: "BR01",
            draweeBranchCode: "BR02",
            amount: 250_000m);

        testKey.Should().NotBeNullOrWhiteSpace();
        testKey.Should().StartWith("TK-");

        // Verification with exact match
        DdTestKeyCalculator.VerifyTestKey(
            testKey: testKey,
            issueDate: _today,
            issuingBranchCode: "BR01",
            draweeBranchCode: "BR02",
            amount: 250_000m).Should().BeTrue();

        // Verification with forged key or altered amount fails
        DdTestKeyCalculator.VerifyTestKey(
            testKey: testKey,
            issueDate: _today,
            issuingBranchCode: "BR01",
            draweeBranchCode: "BR02",
            amount: 500_000m).Should().BeFalse();
    }

    [Fact]
    public void DemandDraft_EncashmentRequiresValidTestKey()
    {
        var dd = new PaymentInstrument(
            instrumentId: "DD-002",
            instrumentType: PaymentInstrumentType.DemandDraft,
            instrumentNumber: "DD-0005003",
            issuingBranchCode: "BR01",
            draweeBranchCode: "BR02",
            amount: Money.Bdt(100_000m),
            payeeName: "Merchant X",
            remitterName: "Buyer Y",
            remitterAccountNumber: "0100123456789",
            isWalkInCustomer: false,
            walkInNid: null,
            walkInPhotoId: null,
            walkInMobile: null,
            transactionPurpose: "Goods Purchase",
            issueDate: _today,
            issuedByUserId: "MAKER_01",
            authorizedByUserId: "CHECKER_01");

        dd.DdTestKey.Should().NotBeNullOrWhiteSpace();
        dd.DrawingAdviceTransmitted.Should().BeTrue();

        // Act - Attempt encashment with no test key
        Action actNoKey = () => dd.MarkPaid(
            paidDate: _today,
            paidByUserId: "TELLER_02",
            voucherNumber: "VCH-ENC-01",
            presentedTestKey: null);

        actNoKey.Should().Throw<InvariantViolationException>()
            .Which.InvariantName.Should().Be("DemandDraftTestKeyMismatchInvariant");

        // Act - Attempt encashment with forged test key
        Action actWrongKey = () => dd.MarkPaid(
            paidDate: _today,
            paidByUserId: "TELLER_02",
            voucherNumber: "VCH-ENC-01",
            presentedTestKey: "TK-999999");

        actWrongKey.Should().Throw<InvariantViolationException>()
            .Which.InvariantName.Should().Be("DemandDraftTestKeyMismatchInvariant");

        // Act - Correct test key succeeds
        dd.MarkPaid(_today, "TELLER_02", "VCH-ENC-01", dd.DdTestKey);
        dd.Status.Should().Be(PaymentInstrumentStatus.Paid);
        dd.PaidDate.Should().Be(_today);
        dd.PaidByUserId.Should().Be("TELLER_02");
    }

    // =========================================================================
    // 5. CANCELLATION, REFUND & DUPLICATE ISSUANCE (BRPD 1.5.6-i, 3.11.2, 3.12.2)
    // =========================================================================

    [Fact]
    public void PaymentOrder_CancelAndRefund_MarksCancelled()
    {
        var po = new PaymentInstrument(
            instrumentId: "PO-CANCEL-01",
            instrumentType: PaymentInstrumentType.PayOrder,
            instrumentNumber: "PO-0002001",
            issuingBranchCode: "BR01",
            draweeBranchCode: null,
            amount: Money.Bdt(50_000m),
            payeeName: "Vendor Inc",
            remitterName: "Buyer ABC",
            remitterAccountNumber: "0100123456789",
            isWalkInCustomer: false,
            walkInNid: null,
            walkInPhotoId: null,
            walkInMobile: null,
            transactionPurpose: "Contract Cancellation",
            issueDate: _today,
            issuedByUserId: "MAKER_01",
            authorizedByUserId: "CHECKER_01");

        po.Cancel(
            cancelledDate: _today,
            cancelledByUserId: "OFFICER_03",
            remarks: "Original PO surrendered by remitter with refund application",
            cancellationFee: 100m);

        po.Status.Should().Be(PaymentInstrumentStatus.Cancelled);
        po.CancelledDate.Should().Be(_today);
        po.CancellationFeeDeducted.Should().Be(100m);

        // Attempting to cancel again throws
        Action actReCancel = () => po.Cancel(_today, "OFFICER_03", "Retry cancel", 100m);
        actReCancel.Should().Throw<InvariantViolationException>()
            .Which.InvariantName.Should().Be("InstrumentCancellationStatusInvariant");
    }

    [Fact]
    public void DuplicateIssuance_MissingPoliceGdOrIndemnity_ThrowsInvariantViolationException()
    {
        var originalPo = new PaymentInstrument(
            instrumentId: "ORIGINAL-PO-01",
            instrumentType: PaymentInstrumentType.PayOrder,
            instrumentNumber: "PO-0002002",
            issuingBranchCode: "BR01",
            draweeBranchCode: null,
            amount: Money.Bdt(50_000m),
            payeeName: "Vendor Inc",
            remitterName: "Buyer ABC",
            remitterAccountNumber: "0100123456789",
            isWalkInCustomer: false,
            walkInNid: null,
            walkInPhotoId: null,
            walkInMobile: null,
            transactionPurpose: "Security Deposit",
            issueDate: _today,
            issuedByUserId: "MAKER_01",
            authorizedByUserId: "CHECKER_01");

        Action actNoGd = () => PaymentInstrument.CreateDuplicate(
            newInstrumentId: "PO-DUP-01",
            newInstrumentNumber: "PO-0002003",
            original: originalPo,
            policeGdNumber: "", // MISSING GD
            indemnityBondNumber: "BOND-999",
            issuedByUserId: "MAKER_01",
            authorizedByUserId: "CHECKER_01");

        actNoGd.Should().Throw<ArgumentException>();

        Action actNoBond = () => PaymentInstrument.CreateDuplicate(
            newInstrumentId: "PO-DUP-02",
            newInstrumentNumber: "PO-0002004",
            original: originalPo,
            policeGdNumber: "GD-1234/2026",
            indemnityBondNumber: "", // MISSING BOND
            issuedByUserId: "MAKER_01",
            authorizedByUserId: "CHECKER_01");

        actNoBond.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DuplicateIssuance_WithValidGdAndIndemnity_MarksOriginalLostAndCreatesDuplicate()
    {
        var originalPo = new PaymentInstrument(
            instrumentId: "ORIGINAL-PO-001",
            instrumentType: PaymentInstrumentType.PayOrder,
            instrumentNumber: "PO-0003001",
            issuingBranchCode: "BR01",
            draweeBranchCode: null,
            amount: Money.Bdt(150_000m),
            payeeName: "Board of Secondary Education",
            remitterName: "School Headmaster",
            remitterAccountNumber: "0100123456789",
            isWalkInCustomer: false,
            walkInNid: null,
            walkInPhotoId: null,
            walkInMobile: null,
            transactionPurpose: "Examination Registration Fees",
            issueDate: _today.AddDays(-10),
            issuedByUserId: "MAKER_01",
            authorizedByUserId: "CHECKER_01");

        var duplicatePo = PaymentInstrument.CreateDuplicate(
            newInstrumentId: "DUP-PO-001",
            newInstrumentNumber: "PO-0003002",
            original: originalPo,
            policeGdNumber: "GD-5678/2026",
            indemnityBondNumber: "INDEMNITY-BOND-44321",
            issuedByUserId: "MAKER_02",
            authorizedByUserId: "CHECKER_02");

        originalPo.Status.Should().Be(PaymentInstrumentStatus.DuplicateIssued);

        duplicatePo.Status.Should().Be(PaymentInstrumentStatus.Issued);
        duplicatePo.DuplicateOfInstrumentId.Should().Be("ORIGINAL-PO-001");
        duplicatePo.PoliceGdNumber.Should().Be("GD-5678/2026");
        duplicatePo.IndemnityBondNumber.Should().Be("INDEMNITY-BOND-44321");
        duplicatePo.Amount.Should().Be(originalPo.Amount);
        duplicatePo.PayeeName.Should().Be(originalPo.PayeeName);
    }
}
