// ARCHITECTURAL INTENT: Domain Unit Tests for Telegraphic Transfer (TT) Lifecycle,
// Slab-based Commission & Telecom Message Charge, 15% VAT, Deterministic Cryptographic Test Keys,
// Telephonic Callback Logging, and Responding Advice Settlement
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 3 (3.13) and Module 1 (1.6.5).

using System;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.TelegraphicTransfers;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class TelegraphicTransferUnitTests
{
    private readonly DateOnly _today = new(2026, 11, 13);

    // =========================================================================
    // 1. SLAB-BASED COMMISSION, MESSAGE CHARGE & 15% VAT TESTS (BRPD 3.13.1-i)
    // =========================================================================

    [Theory]
    [InlineData(1000, 30.00, 20.00, 7.50, TtCommissionSlab.UpTo25K)]
    [InlineData(25000, 30.00, 20.00, 7.50, TtCommissionSlab.UpTo25K)]
    [InlineData(25000.01, 60.00, 30.00, 13.50, TtCommissionSlab.From25KTo100K)]
    [InlineData(100000, 60.00, 30.00, 13.50, TtCommissionSlab.From25KTo100K)]
    [InlineData(100000.01, 120.00, 50.00, 25.50, TtCommissionSlab.From100KTo500K)]
    [InlineData(500000, 120.00, 50.00, 25.50, TtCommissionSlab.From100KTo500K)]
    [InlineData(500000.01, 250.00, 50.00, 45.00, TtCommissionSlab.From500KTo1M)]
    [InlineData(1000000, 250.00, 50.00, 45.00, TtCommissionSlab.From500KTo1M)]
    [InlineData(1000000.01, 350.00, 50.00, 60.00, TtCommissionSlab.Above1M)]
    [InlineData(5000000, 350.00, 50.00, 60.00, TtCommissionSlab.Above1M)]
    public void TtFeeCalculator_CalculatesCorrectSlabFeesAndVat(
        decimal amount,
        decimal expectedCommission,
        decimal expectedMsgCharge,
        decimal expectedVat,
        TtCommissionSlab expectedSlab)
    {
        var (commission, messageCharge, vat, slab) = TtFeeCalculator.Calculate(amount);

        commission.Should().Be(expectedCommission);
        messageCharge.Should().Be(expectedMsgCharge);
        vat.Should().Be(expectedVat);
        slab.Should().Be(expectedSlab);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void TtFeeCalculator_ZeroOrNegativeAmount_ThrowsArgumentOutOfRangeException(decimal invalidAmount)
    {
        var act = () => TtFeeCalculator.Calculate(invalidAmount);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // =========================================================================
    // 2. DETERMINISTIC CRYPTOGRAPHIC TEST KEY TESTS (BRPD 3.13.1-iii, 3.13.2-ii)
    // =========================================================================

    [Fact]
    public void TtTestKeyCalculator_GeneratesConsistentDeterministicKey()
    {
        var key1 = TtTestKeyCalculator.GenerateTestKey(
            _today, "BR01", "BR02", 75000.00m);

        var key2 = TtTestKeyCalculator.GenerateTestKey(
            _today, "BR01", "BR02", 75000.00m);

        key1.Should().NotBeNullOrWhiteSpace();
        key1.Should().StartWith("TT-TEST-");
        key1.Should().Be(key2); // Deterministic
    }

    [Fact]
    public void TtTestKeyCalculator_DifferentAttributes_ProduceDifferentKey()
    {
        var key1 = TtTestKeyCalculator.GenerateTestKey(
            _today, "BR01", "BR02", 75000.00m);

        var keyDifferentAmount = TtTestKeyCalculator.GenerateTestKey(
            _today, "BR01", "BR02", 75000.01m);

        var keyDifferentBranch = TtTestKeyCalculator.GenerateTestKey(
            _today, "BR01", "BR03", 75000.00m);

        var keyDifferentDate = TtTestKeyCalculator.GenerateTestKey(
            _today.AddDays(1), "BR01", "BR02", 75000.00m);

        key1.Should().NotBe(keyDifferentAmount);
        key1.Should().NotBe(keyDifferentBranch);
        key1.Should().NotBe(keyDifferentDate);
    }

    [Fact]
    public void TtTestKeyCalculator_VerifyTestKey_ValidKeyReturnsTrue()
    {
        var testKey = TtTestKeyCalculator.GenerateTestKey(
            _today, "BR01", "BR02", 150000.00m);

        var isValid = TtTestKeyCalculator.VerifyTestKey(
            testKey, _today, "BR01", "BR02", 150000.00m);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void TtTestKeyCalculator_VerifyTestKey_TamperedKeyOrAmountReturnsFalse()
    {
        var testKey = TtTestKeyCalculator.GenerateTestKey(
            _today, "BR01", "BR02", 150000.00m);

        var tamperedKey = TtTestKeyCalculator.VerifyTestKey(
            "TT-TEST-999999", _today, "BR01", "BR02", 150000.00m);

        var tamperedAmount = TtTestKeyCalculator.VerifyTestKey(
            testKey, _today, "BR01", "BR02", 150000.01m);

        tamperedKey.Should().BeFalse();
        tamperedAmount.Should().BeFalse();
    }

    // =========================================================================
    // 3. MANDATORY ATTRIBUTES & INVARIANT TESTS (BRPD 3.13.1-ii..vi, 1.3.18)
    // =========================================================================

    [Fact]
    public void TelegraphicTransfer_ValidIssuance_SetsAllFieldsAndInitialState()
    {
        var tt = new TelegraphicTransfer(
            ttId: "TT-001",
            ttNumber: "TT-BR01-20261113-000001",
            issueDate: _today,
            issuingBranchCode: "BR01",
            draweeBranchCode: "BR02",
            amount: Money.Bdt(50000m),
            payeeName: "Rahim Chowdhury",
            payeeAccountNumber: "0100123456",
            remitterName: "Karim Ahmed",
            remitterAccountNumber: "0200987654",
            transactionPurpose: "Family Maintenance",
            sanctionScreeningPassed: true,
            makerUserId: "OFFICER_01",
            checkerUserId: "MANAGER_01");

        tt.Id.Should().Be("TT-001");
        tt.TtNumber.Should().Be("TT-BR01-20261113-000001");
        tt.IssueDate.Should().Be(_today);
        tt.IssuingBranchCode.Should().Be("BR01");
        tt.DraweeBranchCode.Should().Be("BR02");
        tt.Amount.Amount.Should().Be(50000m);
        tt.PayeeName.Should().Be("Rahim Chowdhury");
        tt.CommissionAmount.Should().Be(60m);
        tt.MessageChargeAmount.Should().Be(30m);
        tt.VatAmount.Should().Be(13.50m);
        tt.TtTestKey.Should().StartWith("TT-TEST-");
        tt.Status.Should().Be(TtStatus.Issued);
        tt.SanctionScreeningPassed.Should().BeTrue();
        tt.MakerUserId.Should().Be("OFFICER_01");
        tt.CheckerUserId.Should().Be("MANAGER_01");
    }

    [Fact]
    public void TelegraphicTransfer_IssuingAndDraweeBranchSame_ThrowsTtInterBranchRequiredInvariant()
    {
        var act = () => new TelegraphicTransfer(
            ttId: "TT-001",
            ttNumber: "TT-BR01-20261113-000001",
            issueDate: _today,
            issuingBranchCode: "BR01",
            draweeBranchCode: "BR01", // Same branch
            amount: Money.Bdt(50000m),
            payeeName: "Rahim Chowdhury",
            payeeAccountNumber: null,
            remitterName: null,
            remitterAccountNumber: null,
            transactionPurpose: null,
            sanctionScreeningPassed: true,
            makerUserId: "OFFICER_01",
            checkerUserId: "MANAGER_01");

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("TtInterBranchRequiredInvariant");
    }

    [Fact]
    public void TelegraphicTransfer_MakerEqualsChecker_ThrowsTtMakerCheckerInvariant()
    {
        var act = () => new TelegraphicTransfer(
            ttId: "TT-001",
            ttNumber: "TT-BR01-20261113-000001",
            issueDate: _today,
            issuingBranchCode: "BR01",
            draweeBranchCode: "BR02",
            amount: Money.Bdt(50000m),
            payeeName: "Rahim Chowdhury",
            payeeAccountNumber: null,
            remitterName: null,
            remitterAccountNumber: null,
            transactionPurpose: null,
            sanctionScreeningPassed: true,
            makerUserId: "OFFICER_SAME",
            checkerUserId: "OFFICER_SAME"); // Violates maker-checker

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("TtMakerCheckerInvariant");
    }

    [Fact]
    public void TelegraphicTransfer_SanctionScreeningNotPassed_ThrowsTtSanctionScreeningRequiredInvariant()
    {
        var act = () => new TelegraphicTransfer(
            ttId: "TT-001",
            ttNumber: "TT-BR01-20261113-000001",
            issueDate: _today,
            issuingBranchCode: "BR01",
            draweeBranchCode: "BR02",
            amount: Money.Bdt(50000m),
            payeeName: "Rahim Chowdhury",
            payeeAccountNumber: null,
            remitterName: null,
            remitterAccountNumber: null,
            transactionPurpose: null,
            sanctionScreeningPassed: false, // Sanction clearance missing
            makerUserId: "OFFICER_01",
            checkerUserId: "MANAGER_01");

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("TtSanctionScreeningRequiredInvariant");
    }

    [Fact]
    public void TelegraphicTransfer_EmptyPayeeName_ThrowsArgumentException()
    {
        var act = () => new TelegraphicTransfer(
            ttId: "TT-001",
            ttNumber: "TT-BR01-20261113-000001",
            issueDate: _today,
            issuingBranchCode: "BR01",
            draweeBranchCode: "BR02",
            amount: Money.Bdt(50000m),
            payeeName: "   ", // Empty payee name
            payeeAccountNumber: null,
            remitterName: null,
            remitterAccountNumber: null,
            transactionPurpose: null,
            sanctionScreeningPassed: true,
            makerUserId: "OFFICER_01",
            checkerUserId: "MANAGER_01");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void TelegraphicTransfer_NonPositiveAmount_ThrowsArgumentOutOfRangeException(decimal invalidAmount)
    {
        var act = () => new TelegraphicTransfer(
            ttId: "TT-001",
            ttNumber: "TT-BR01-20261113-000001",
            issueDate: _today,
            issuingBranchCode: "BR01",
            draweeBranchCode: "BR02",
            amount: Money.Bdt(invalidAmount),
            payeeName: "Rahim Chowdhury",
            payeeAccountNumber: null,
            remitterName: null,
            remitterAccountNumber: null,
            transactionPurpose: null,
            sanctionScreeningPassed: true,
            makerUserId: "OFFICER_01",
            checkerUserId: "MANAGER_01");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // =========================================================================
    // 4. ADVICE TRANSMISSION & TELEPHONIC CALLBACK TESTS (BRPD 3.13.1-iv..v)
    // =========================================================================

    [Fact]
    public void MarkAdviceTransmitted_FromIssuedStatus_UpdatesStatusAndTransmissionTimestamp()
    {
        var tt = CreateSampleTt();

        tt.MarkAdviceTransmitted("COMM_OFFICER_01");

        tt.Status.Should().Be(TtStatus.AdviceTransmitted);
        tt.AdviceTransmittedByUserId.Should().Be("COMM_OFFICER_01");
        tt.AdviceTransmittedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void LogTelephonicVerification_FromAdviceTransmitted_UpdatesStatusAndCallbackMetadata()
    {
        var tt = CreateSampleTt();
        tt.MarkAdviceTransmitted("COMM_OFFICER_01");

        tt.LogTelephonicVerification(
            receivingOfficerUserId: "DRAWEE_TELLER_01",
            verifiedWithOfficerName: "Mr. Kabir (Issuing Remittance Incharge)",
            contactNumber: "+8801711000000",
            notes: "Test key verified against secret book formula.");

        tt.Status.Should().Be(TtStatus.TelephonicallyVerified);
        tt.TelephonicVerifiedByOfficer.Should().Be("DRAWEE_TELLER_01");
        tt.TelephonicContactNumber.Should().Be("+8801711000000");
        tt.TelephonicNotes.Should().Contain("Mr. Kabir (Issuing Remittance Incharge)");
        tt.TelephonicallyVerifiedAtUtc.Should().NotBeNull();
    }

    // =========================================================================
    // 5. RESPONDING ADVICE & SETTLEMENT TESTS (BRPD 3.13.2-ii..iii, 1.6.5)
    // =========================================================================

    [Fact]
    public void RespondAndPay_ValidTestKey_SettlesAndGeneratesRespondingAdvice()
    {
        var tt = CreateSampleTt();
        tt.MarkAdviceTransmitted("COMM_OFFICER_01");

        var respondingDate = _today.AddDays(1);
        tt.RespondAndPay(
            respondedDate: respondingDate,
            respondingOfficerUserId: "DRAWEE_MANAGER",
            respondingVoucherNumber: "VOUCH-RESP-2026-9999",
            presentedTestKey: tt.TtTestKey, // Matches tt.TtTestKey
            destinationAccountOrSundryGl: "0200555666");

        tt.Status.Should().Be(TtStatus.Responded);
        tt.RespondingAdviceNumber.Should().StartWith("TT-RESP-BR02-");
        tt.RespondingOfficerUserId.Should().Be("DRAWEE_MANAGER");
        tt.RespondingVoucherNumber.Should().Be("VOUCH-RESP-2026-9999");
        tt.DestinationAccountOrSundryGl.Should().Be("0200555666");
        tt.RespondedDate.Should().Be(respondingDate);
    }

    [Fact]
    public void RespondAndPay_WrongTestKey_ThrowsTtTestKeyMismatchInvariant()
    {
        var tt = CreateSampleTt();
        tt.MarkAdviceTransmitted("COMM_OFFICER_01");

        var act = () => tt.RespondAndPay(
            respondedDate: _today,
            respondingOfficerUserId: "DRAWEE_MANAGER",
            respondingVoucherNumber: "VOUCH-RESP-2026-9999",
            presentedTestKey: "TT-TEST-999999",
            destinationAccountOrSundryGl: "0200555666");

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("TtTestKeyMismatchInvariant");
        tt.Status.Should().Be(TtStatus.AdviceTransmitted); // Unchanged
    }

    [Fact]
    public void RespondAndPay_AlreadyResponded_ThrowsTtAlreadyRespondedInvariant()
    {
        var tt = CreateSampleTt();
        tt.MarkAdviceTransmitted("COMM_OFFICER_01");
        tt.RespondAndPay(_today, "OFFICER_1", "V1", tt.TtTestKey, "0200");

        var act = () => tt.RespondAndPay(_today, "OFFICER_2", "V2", tt.TtTestKey, "0200");

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("TtAlreadyRespondedInvariant");
    }

    // =========================================================================
    // 6. CANCELLATION TESTS (BRPD 3.13)
    // =========================================================================

    [Fact]
    public void Cancel_WhenIssuedOrAdviceTransmitted_CancelsSuccessfully()
    {
        var tt = CreateSampleTt();
        tt.Cancel(
            cancelledDate: _today,
            cancelledByUserId: "BRANCH_MANAGER",
            remarks: "Remitter requested cancellation prior to responding advice");

        tt.Status.Should().Be(TtStatus.Cancelled);
        tt.CancelledByUserId.Should().Be("BRANCH_MANAGER");
        tt.CancellationRemarks.Should().Contain("Remitter requested cancellation");
        tt.CancelledDate.Should().Be(_today);
    }

    [Fact]
    public void Cancel_WhenAlreadyResponded_ThrowsTtCancellationStatusInvariant()
    {
        var tt = CreateSampleTt();
        tt.MarkAdviceTransmitted("COMM_OFFICER_01");
        tt.RespondAndPay(_today, "OFFICER_1", "V1", tt.TtTestKey, "0200");

        var act = () => tt.Cancel(_today, "MANAGER", "Try to cancel after paid");

        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("TtCancellationStatusInvariant");
    }

    // =========================================================================
    // HELPER METHODS
    // =========================================================================

    private TelegraphicTransfer CreateSampleTt()
    {
        return new TelegraphicTransfer(
            ttId: "TT-001",
            ttNumber: "TT-BR01-20261113-000001",
            issueDate: _today,
            issuingBranchCode: "BR01",
            draweeBranchCode: "BR02",
            amount: Money.Bdt(75000m),
            payeeName: "Anisur Rahman",
            payeeAccountNumber: "0100112233",
            remitterName: "Mahbub Alam",
            remitterAccountNumber: "0200445566",
            transactionPurpose: "Medical expenses",
            sanctionScreeningPassed: true,
            makerUserId: "MAKER_OFFICER_1",
            checkerUserId: "CHECKER_OFFICER_1");
    }
}
