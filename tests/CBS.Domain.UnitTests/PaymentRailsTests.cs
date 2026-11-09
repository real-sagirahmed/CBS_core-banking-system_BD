using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.PaymentRails;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class PaymentRailsTests
{
    private static AccountNumber CreateValidAccountNumber() => AccountNumber.Generate("001", "120", 1);
    private static RoutingNumber CreateValidRouting() => RoutingNumber.From("015270112"); // Sonali Bank, Motijheel

    [Fact]
    public void RoutingNumber_ValidNineDigits_ParsesBankDistrictBranchCodes()
    {
        var routing = RoutingNumber.From("015270112");
        routing.Value.Should().Be("015270112");
        routing.BankCode.Should().Be("015");
        routing.DistrictCode.Should().Be("270");
        routing.BranchCode.Should().Be("112");
    }

    [Theory]
    [InlineData("01527011")]   // 8 digits
    [InlineData("0152701123")] // 10 digits
    [InlineData("01527ABCD")]  // Alphabetic
    public void RoutingNumber_InvalidLengthOrFormat_ThrowsInvariantViolation(string invalid)
    {
        var action = () => RoutingNumber.From(invalid);
        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*InvalidRoutingNumberInvariant*");
    }

    [Fact]
    public void MicrLine_ValidInputs_FormatsE13bStandard()
    {
        var micr = new MicrLine(
            "001234",
            CreateValidRouting(),
            CreateValidAccountNumber(),
            "10");

        micr.ChequeNumber.Should().Be("001234");
        micr.TransactionCode.Should().Be("10");
        micr.FormattedMicr.Should().Contain("T001234T 015270112A");
    }

    [Fact]
    public void MicrLine_InvalidChequeNumber_ThrowsInvariantViolation()
    {
        var action = () => new MicrLine(
            "12345", // 5 digits instead of 6
            CreateValidRouting(),
            CreateValidAccountNumber(),
            "10");

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*InvalidChequeNumberInvariant*");
    }

    [Fact]
    public void ChequeClearing_AmountUnderFiveHundredThousand_AssignedRegularValueSession()
    {
        var micr = new MicrLine("001234", CreateValidRouting(), CreateValidAccountNumber(), "10");
        var cheque = new ChequeClearingItem(
            Guid.NewGuid(),
            "BATCH-BACH-001",
            ClearingDirection.Outward,
            micr,
            Money.Bdt(499999m),
            CreateValidRouting(),
            RoutingNumber.From("085270119"), // Pubali Bank
            "0011200000029",
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 9));

        cheque.Session.Should().Be(ClearingSessionType.RegularValue);
        cheque.Status.Should().Be(ChequeClearingStatus.Presented);
    }

    [Fact]
    public void ChequeClearing_AmountFiveHundredThousandOrAbove_AssignedHighValueSession()
    {
        var micr = new MicrLine("001234", CreateValidRouting(), CreateValidAccountNumber(), "10");
        var cheque = new ChequeClearingItem(
            Guid.NewGuid(),
            "BATCH-BACH-002",
            ClearingDirection.Outward,
            micr,
            Money.Bdt(500000m),
            CreateValidRouting(),
            RoutingNumber.From("085270119"),
            "0011200000029",
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 9));

        cheque.Session.Should().Be(ClearingSessionType.HighValue);
        cheque.Status.Should().Be(ChequeClearingStatus.Presented);
    }

    [Fact]
    public void ChequeClearing_StaleCheque_Exceeding180Days_AutomaticallyReturned()
    {
        var micr = new MicrLine("001234", CreateValidRouting(), CreateValidAccountNumber(), "10");
        // Issue date is 200 days before presentation date
        var issueDate = new DateOnly(2026, 4, 1);
        var presentDate = new DateOnly(2026, 11, 9);

        var cheque = new ChequeClearingItem(
            Guid.NewGuid(),
            "BATCH-BACH-003",
            ClearingDirection.Inward,
            micr,
            Money.Bdt(50000m),
            RoutingNumber.From("085270119"),
            CreateValidRouting(),
            "0011200000029",
            issueDate,
            presentDate);

        cheque.Status.Should().Be(ChequeClearingStatus.Returned);
        cheque.ReturnReason.Should().Be(BachReturnReason.PostDatedOrStaleCheque);
    }

    [Fact]
    public void ChequeClearing_PostDatedCheque_AutomaticallyReturned()
    {
        var micr = new MicrLine("001234", CreateValidRouting(), CreateValidAccountNumber(), "10");
        // Issue date is future relative to presentation date
        var issueDate = new DateOnly(2026, 11, 15);
        var presentDate = new DateOnly(2026, 11, 9);

        var cheque = new ChequeClearingItem(
            Guid.NewGuid(),
            "BATCH-BACH-004",
            ClearingDirection.Inward,
            micr,
            Money.Bdt(50000m),
            RoutingNumber.From("085270119"),
            CreateValidRouting(),
            "0011200000029",
            issueDate,
            presentDate);

        cheque.Status.Should().Be(ChequeClearingStatus.Returned);
        cheque.ReturnReason.Should().Be(BachReturnReason.PostDatedOrStaleCheque);
    }

    [Fact]
    public void ChequeClearing_Settle_TransitionsToSettled()
    {
        var micr = new MicrLine("001234", CreateValidRouting(), CreateValidAccountNumber(), "10");
        var cheque = new ChequeClearingItem(
            Guid.NewGuid(),
            "BATCH-BACH-005",
            ClearingDirection.Outward,
            micr,
            Money.Bdt(25000m),
            CreateValidRouting(),
            RoutingNumber.From("085270119"),
            "0011200000029",
            new DateOnly(2026, 11, 5),
            new DateOnly(2026, 11, 9));

        cheque.Settle(new DateOnly(2026, 11, 9));

        cheque.Status.Should().Be(ChequeClearingStatus.Settled);
        cheque.SettlementDate.Should().Be(new DateOnly(2026, 11, 9));
    }

    [Fact]
    public void BeftnBatchItem_FullLifecycle_Queue_SendToBb_Settle()
    {
        var beftn = new BeftnBatchItem(
            Guid.NewGuid(),
            "BEFTN-OUT-20261109-01",
            ClearingDirection.Outward,
            BeftnTransactionType.CreditTransfer,
            Money.Bdt(75000m),
            CreateValidRouting(),
            "0011200000018",
            RoutingNumber.From("085270119"),
            "0851200000099",
            "Sagir Ahmed",
            "Monthly Salary Nov 2026",
            new DateOnly(2026, 11, 9));

        beftn.Status.Should().Be(BeftnStatus.Queued);

        beftn.SendToBangladeshBank();
        beftn.Status.Should().Be(BeftnStatus.SentToBb);

        beftn.Settle(new DateOnly(2026, 11, 10));
        beftn.Status.Should().Be(BeftnStatus.Settled);
        beftn.SettlementDate.Should().Be(new DateOnly(2026, 11, 10));
    }

    [Fact]
    public void RtgsTransaction_CustomerCreditTransfer_UnderOneHundredThousand_ThrowsThresholdInvariant()
    {
        // 99,999 BDT is below 100,000 BDT minimum for customer RTGS (BRPD 1.5.5)
        var action = () => new RtgsTransaction(
            Guid.NewGuid(),
            "RTGS-E2E-0001",
            RtgsMessageType.Pacs008_CustomerCreditTransfer,
            Money.Bdt(99999m),
            CreateValidRouting(),
            "0011200000018",
            RoutingNumber.From("085270119"),
            "0851200000099",
            "Creditor Corp",
            "Urgent Raw Material Payment",
            DateTime.UtcNow);

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*RtgsMinimumThresholdInvariant*");
    }

    [Fact]
    public void RtgsTransaction_CustomerCreditTransfer_OneHundredThousandOrAbove_SucceedsAndSettles()
    {
        var tx = new RtgsTransaction(
            Guid.NewGuid(),
            "RTGS-E2E-0002",
            RtgsMessageType.Pacs008_CustomerCreditTransfer,
            Money.Bdt(5000000m), // 5,000,000 BDT
            CreateValidRouting(),
            "0011200000018",
            RoutingNumber.From("085270119"),
            "0851200000099",
            "Steel Mills Ltd",
            "Invoice #ST-9988",
            DateTime.UtcNow);

        tx.Status.Should().Be(RtgsStatus.Initiated);

        var settleTime = DateTime.UtcNow;
        tx.SettleAtBangladeshBank("BB-RTGS-SETTLE-88776655", settleTime);

        tx.Status.Should().Be(RtgsStatus.SettledAtBangladeshBank);
        tx.BangladeshBankReference.Should().Be("BB-RTGS-SETTLE-88776655");
        tx.SettledAtUtc.Should().Be(settleTime);
    }
}
