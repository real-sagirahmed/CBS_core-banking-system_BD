// ARCHITECTURAL INTENT: Domain Unit Tests locking Back-Value Date Window Invariants,
// Dual-Custody Approval, Retroactive Interest Differential Calculations, and Linked Cascade Reversals
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 1 (1.3.4 & 1.3.6).

using System;
using CBS.Domain.BackValue;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class BackValueAndCascadeReversalUnitTests
{
    private readonly DateOnly _bookingDate = new(2026, 11, 13);

    [Fact]
    public void BackValueRequest_Should_Reject_Future_Value_Date()
    {
        // Arrange
        var futureDate = _bookingDate.AddDays(1);

        // Act
        Action act = () => new BackValueTransactionRequest(
            requestId: "BV-001",
            targetType: BackValueTargetType.DepositAccount,
            targetIdentifier: "0100123456789",
            branchCode: "BR-001",
            bookingDate: _bookingDate,
            valueDate: futureDate,
            amount: Money.Bdt(50_000m),
            transactionNature: DebitCredit.Credit,
            reason: "Attempting future value entry",
            regulatoryReference: "MEMO-123",
            makerUserId: "MAKER-01");

        // Assert
        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("FutureValueDateProhibitedInvariant");
        ex.RegulatoryReference.Should().Contain("1.3.4-i");
        ex.Message.Should().Contain("cannot be later than current Booking Date");
    }

    [Fact]
    public void BackValueRequest_Should_Reject_Date_Exceeding_30_Days_Window()
    {
        // Arrange (35 days in past)
        var oldDate = _bookingDate.AddDays(-35);

        // Act
        Action act = () => new BackValueTransactionRequest(
            requestId: "BV-002",
            targetType: BackValueTargetType.DepositAccount,
            targetIdentifier: "0100123456789",
            branchCode: "BR-001",
            bookingDate: _bookingDate,
            valueDate: oldDate,
            amount: Money.Bdt(50_000m),
            transactionNature: DebitCredit.Credit,
            reason: "Too far in past",
            regulatoryReference: "MEMO-124",
            makerUserId: "MAKER-01");

        // Assert
        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("BackValueWindowExceededInvariant");
        ex.RegulatoryReference.Should().Contain("1.3.4-i");
        ex.Message.Should().Contain("exceeds the maximum statutory back-value window of 30 calendar days");
    }

    [Fact]
    public void BackValueRequest_Should_Allow_Valid_Date_Within_Window()
    {
        // Arrange (15 days in past)
        var validPastDate = _bookingDate.AddDays(-15);

        // Act
        var request = new BackValueTransactionRequest(
            requestId: "BV-003",
            targetType: BackValueTargetType.DepositAccount,
            targetIdentifier: "0100123456789",
            branchCode: "BR-001",
            bookingDate: _bookingDate,
            valueDate: validPastDate,
            amount: Money.Bdt(100_000m),
            transactionNature: DebitCredit.Credit,
            reason: "Delayed inward cash letter processing",
            regulatoryReference: "BRPD-AUTH-2026-99",
            makerUserId: "MAKER-01");

        // Assert
        request.Status.Should().Be(BackValueStatus.PendingApproval);
        request.DaysVariance.Should().Be(15);
        request.ValueDate.Should().Be(validPastDate);
        request.BookingDate.Should().Be(_bookingDate);
    }

    [Theory]
    [InlineData("", "MEMO-REF")]
    [InlineData("Valid reason", "")]
    [InlineData("   ", "   ")]
    public void BackValueRequest_Should_Require_Reason_And_Regulatory_Reference(string reason, string regRef)
    {
        // Act
        Action act = () => new BackValueTransactionRequest(
            requestId: "BV-004",
            targetType: BackValueTargetType.DepositAccount,
            targetIdentifier: "0100123456789",
            branchCode: "BR-001",
            bookingDate: _bookingDate,
            valueDate: _bookingDate.AddDays(-5),
            amount: Money.Bdt(20_000m),
            transactionNature: DebitCredit.Credit,
            reason: reason,
            regulatoryReference: regRef,
            makerUserId: "MAKER-01");

        // Assert
        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("BackValueJustificationRequiredInvariant");
        ex.RegulatoryReference.Should().Contain("1.3.4-i, iii");
    }

    [Fact]
    public void BackValueRequest_Should_Prevent_Maker_Self_Approval()
    {
        // Arrange
        var request = new BackValueTransactionRequest(
            requestId: "BV-005",
            targetType: BackValueTargetType.GeneralLedgerVoucher,
            targetIdentifier: "GL-10101-01",
            branchCode: "BR-001",
            bookingDate: _bookingDate,
            valueDate: _bookingDate.AddDays(-7),
            amount: Money.Bdt(500_000m),
            transactionNature: DebitCredit.Debit,
            reason: "Audit adjustment",
            regulatoryReference: "AUDIT-MEMO-55",
            makerUserId: "MAKER-01");

        // Act & Assert: Maker attempting to approve own request
        Action selfApprove = () => request.Approve(
            checkerUserId: "MAKER-01",
            remarks: "Self approving",
            executedVoucherNumber: "VCH-01",
            retroactiveInterestCalculated: 0m,
            retroactiveInterestVoucherNumber: null);

        var ex = selfApprove.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("BackValueMakerSelfApprovalInvariant");
        ex.RegulatoryReference.Should().Contain("1.3.4-i, iv");
        ex.Message.Should().Contain("cannot authorize their own back-value dated transaction");
    }

    [Fact]
    public void BackValueRequest_Should_Prevent_Maker_Self_Rejection()
    {
        // Arrange
        var request = new BackValueTransactionRequest(
            requestId: "BV-006",
            targetType: BackValueTargetType.GeneralLedgerVoucher,
            targetIdentifier: "GL-10101-01",
            branchCode: "BR-001",
            bookingDate: _bookingDate,
            valueDate: _bookingDate.AddDays(-7),
            amount: Money.Bdt(500_000m),
            transactionNature: DebitCredit.Debit,
            reason: "Audit adjustment",
            regulatoryReference: "AUDIT-MEMO-55",
            makerUserId: "MAKER-01");

        // Act & Assert: Maker attempting to reject own request
        Action selfReject = () => request.Reject(
            checkerUserId: "maker-01",
            rejectionReason: "Self reject");

        var ex = selfReject.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("BackValueMakerSelfApprovalInvariant");
    }

    [Fact]
    public void BackValueRequest_Should_Allow_Independent_Checker_Approval()
    {
        // Arrange
        var request = new BackValueTransactionRequest(
            requestId: "BV-007",
            targetType: BackValueTargetType.DepositAccount,
            targetIdentifier: "0100998877665",
            branchCode: "BR-001",
            bookingDate: _bookingDate,
            valueDate: _bookingDate.AddDays(-10),
            amount: Money.Bdt(200_000m),
            transactionNature: DebitCredit.Credit,
            reason: "Clearing settlement back date",
            regulatoryReference: "CLEAR-REF-77",
            makerUserId: "MAKER-01");

        // Act: Independent checker approves
        request.Approve(
            checkerUserId: "CHECKER-02",
            remarks: "Dual custody verified",
            executedVoucherNumber: "VCH-BV-7788",
            retroactiveInterestCalculated: 328.77m,
            retroactiveInterestVoucherNumber: "VCH-INTR-7788");

        // Assert
        request.Status.Should().Be(BackValueStatus.Approved);
        request.CheckerUserId.Should().Be("CHECKER-02");
        request.ExecutedVoucherNumber.Should().Be("VCH-BV-7788");
        request.RetroactiveInterestCalculated.Should().Be(328.77m);
        request.RetroactiveInterestVoucherNumber.Should().Be("VCH-INTR-7788");
        request.ActionedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void RetroactiveInterestCalculator_Deposit_Credit_Should_Calculate_Accrued_Interest()
    {
        // Arrange: 100,000 BDT deposit back-dated by 15 days at 6% p.a.
        // Expected: 100,000 * 0.06 * (15 / 365) = 246.58 BDT
        var valueDate = _bookingDate.AddDays(-15);

        // Act
        var result = RetroactiveInterestCalculator.Calculate(
            principalAmount: 100_000m,
            annualInterestRatePercent: 6.00m,
            valueDate: valueDate,
            bookingDate: _bookingDate,
            transactionNature: DebitCredit.Credit,
            isDepositAccount: true);

        // Assert
        result.DifferentialAmount.Should().Be(246.58m);
        result.Direction.Should().Be(AdjustmentDirection.CreditCustomer);
        result.DaysPast.Should().Be(15);
        result.AccountingNarration.Should().Contain("Retroactive deposit interest credited for 15 days");
    }

    [Fact]
    public void RetroactiveInterestCalculator_Deposit_Debit_Should_Calculate_Interest_Clawback()
    {
        // Arrange: 200,000 BDT withdrawal back-dated by 20 days at 5% p.a.
        // Expected: 200,000 * 0.05 * (20 / 365) = 547.95 BDT
        var valueDate = _bookingDate.AddDays(-20);

        // Act
        var result = RetroactiveInterestCalculator.Calculate(
            principalAmount: 200_000m,
            annualInterestRatePercent: 5.00m,
            valueDate: valueDate,
            bookingDate: _bookingDate,
            transactionNature: DebitCredit.Debit,
            isDepositAccount: true);

        // Assert
        result.DifferentialAmount.Should().Be(547.95m);
        result.Direction.Should().Be(AdjustmentDirection.DebitCustomer);
        result.DaysPast.Should().Be(20);
        result.AccountingNarration.Should().Contain("clawback");
    }

    [Fact]
    public void RetroactiveInterestCalculator_Loan_Repayment_Should_Calculate_Interest_Refund()
    {
        // Arrange: 500,000 BDT loan repayment back-dated by 10 days at 9% p.a.
        // Expected: 500,000 * 0.09 * (10 / 365) = 1,232.88 BDT
        var valueDate = _bookingDate.AddDays(-10);

        // Act
        var result = RetroactiveInterestCalculator.Calculate(
            principalAmount: 500_000m,
            annualInterestRatePercent: 9.00m,
            valueDate: valueDate,
            bookingDate: _bookingDate,
            transactionNature: DebitCredit.Credit,
            isDepositAccount: false);

        // Assert
        result.DifferentialAmount.Should().Be(1232.88m);
        result.Direction.Should().Be(AdjustmentDirection.CreditCustomer);
        result.AccountingNarration.Should().Contain("refund");
    }

    [Fact]
    public void LinkedTransactionReversalRecord_Should_Require_Mandatory_Fields()
    {
        // Act
        var record = new LinkedTransactionReversalRecord(
            reversalRecordId: "REV-REC-001",
            originalVoucherId: "VCH-ORIG-100",
            originalVoucherNumber: "001-202611-100",
            reversalReason: "Wrong account credited",
            reversedByUserId: "MAKER-01",
            authorizerUserId: "CHECKER-02",
            parentReversalVoucherNumber: "REV-001-202611-100",
            linkedReversalsJson: "[{\"AuxiliaryVoucherNumber\":\"VCH-FEE-01\",\"Amount\":50.00}]",
            totalReversedAmount: Money.Bdt(50_050m));

        // Assert
        record.IsCascadeComplete.Should().BeTrue();
        record.ReversalRecordId.Should().Be("REV-REC-001");
        record.TotalReversedAmount.Amount.Should().Be(50_050m);
    }
}
