using CBS.Domain.Cheques;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;

namespace CBS.Domain.UnitTests;

public class ChequeBookLifecycleTests
{
    private static readonly AccountNumber Acc = AccountNumber.From("0101101000010");

    [Fact]
    public void ChequeBook_ThrowsException_WhenTotalLeavesIsNonStandard()
    {
        var act = () => new ChequeBook(
            "BK-001",
            Acc,
            "SB-",
            1001,
            33, // Non-standard leaf count
            "0101",
            new DateOnly(2026, 11, 10));

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Cheque book leaf count '33' must be one of standard denominations*");
    }

    [Fact]
    public void ChequeBook_CreatesSequentialLeavesInAvailableStatus()
    {
        var book = new ChequeBook(
            "BK-002",
            Acc,
            "SB-",
            2001,
            25,
            "0101",
            new DateOnly(2026, 11, 10));

        book.Leaves.Should().HaveCount(25);
        book.StartLeafNumber.Should().Be(2001);
        book.EndLeafNumber.Should().Be(2025);
        book.Leaves.Should().AllSatisfy(l => l.Status.Should().Be(ChequeLeafStatus.Available));

        book.MarkDelivered();
        book.Status.Should().Be(ChequeBookStatus.DeliveredToCustomer);
        book.DeliveredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void ChequeLeaf_AssertCanBeHonored_ThrowsException_WhenStopped()
    {
        var book = new ChequeBook("BK-003", Acc, "SB-", 3001, 10, "0101", new DateOnly(2026, 11, 10));
        var leaf = book.GetLeaf(3005);

        leaf.AssertCanBeHonored(); // Should not throw initially

        book.StopLeaf(3005, StopPaymentReason.LostOrStolen, "Customer wallet lost in transit", "0101");

        leaf.Status.Should().Be(ChequeLeafStatus.Stopped);
        leaf.StopReason.Should().Be(StopPaymentReason.LostOrStolen);

        var act = () => leaf.AssertCanBeHonored();
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Cheque Leaf '3005' has active Stop Payment instruction*");
    }

    [Fact]
    public void ChequeLeaf_AssertCanBeHonored_ThrowsException_WhenAlreadyPaid()
    {
        var book = new ChequeBook("BK-004", Acc, "SB-", 4001, 10, "0101", new DateOnly(2026, 11, 10));
        var leaf = book.GetLeaf(4001);

        leaf.MarkPaid("VCH-PAID-001");
        leaf.Status.Should().Be(ChequeLeafStatus.Paid);

        // Subsequent attempt to honor duplicate payment
        var act = () => leaf.AssertCanBeHonored();
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Cheque Leaf '4001' has already been paid*Duplicate payment prohibited*");

        // Attempt to stop an already paid leaf
        var actStop = () => book.StopLeaf(4001, StopPaymentReason.CustomerRequest, "Try to stop", "0101");
        actStop.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot stop payment on cheque leaf '4001' because it is already Paid*");
    }

    [Fact]
    public void ChequeLeaf_RevokeStopPayment_RestoresLeafToAvailable()
    {
        var book = new ChequeBook("BK-005", Acc, "SB-", 5001, 10, "0101", new DateOnly(2026, 11, 10));
        book.StopLeaf(5002, StopPaymentReason.CommercialDispute, "Pending invoice dispute", "0101");

        var leaf = book.GetLeaf(5002);
        leaf.Status.Should().Be(ChequeLeafStatus.Stopped);

        book.RevokeStopPayment(5002, "OFFICER_01", "Invoice settlement dispute resolved by client");
        leaf.Status.Should().Be(ChequeLeafStatus.Available);
        leaf.RevokedByOfficerId.Should().Be("OFFICER_01");

        // Can now be honored
        var act = () => leaf.AssertCanBeHonored();
        act.Should().NotThrow();
    }
}
