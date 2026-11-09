using CBS.Domain.BranchCash;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class RemittanceInstrumentTests
{
    [Theory]
    [InlineData(10000, 25, 3.75, 10028.75)]       // <= 25k -> 25 comm, 3.75 VAT
    [InlineData(50000, 50, 7.50, 50057.50)]       // <= 100k -> 50 comm, 7.50 VAT
    [InlineData(250000, 100, 15.00, 250115.00)]   // <= 500k -> 100 comm, 15.00 VAT
    [InlineData(1000000, 200, 30.00, 1000230.00)] // > 500k -> 200 comm, 30.00 VAT
    public void RemittanceInstrument_StatutoryTieredCommissionAndVat_CalculatesAccurately(
        decimal principal,
        decimal expectedCommission,
        decimal expectedVat,
        decimal expectedTotal)
    {
        var instrument = new RemittanceInstrument(
            Guid.NewGuid(),
            "PO-0010001",
            RemittanceInstrumentType.PayOrder,
            "BR001",
            Money.Bdt(principal),
            "Dhaka Electric Supply Company",
            "CIF99887766",
            walkInDetails: null,
            makerUserId: "MAKER_01",
            checkerUserId: "CHECKER_01",
            issueDate: new DateOnly(2026, 11, 9));

        instrument.Commission.Should().Be(Money.Bdt(expectedCommission));
        instrument.VatAmount.Should().Be(Money.Bdt(expectedVat));
        instrument.TotalPayable.Should().Be(Money.Bdt(expectedTotal));
        instrument.Status.Should().Be(RemittanceInstrumentStatus.Issued);
    }

    [Fact]
    public void RemittanceInstrument_MakerEqualsChecker_ThrowsMakerCheckerSeparation()
    {
        var action = () => new RemittanceInstrument(
            Guid.NewGuid(),
            "PO-0010002",
            RemittanceInstrumentType.PayOrder,
            "BR001",
            Money.Bdt(10000m),
            "Beneficiary",
            "CIF123",
            walkInDetails: null,
            makerUserId: "OFFICER_01",
            checkerUserId: "OFFICER_01",
            issueDate: new DateOnly(2026, 11, 9));

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*MakerCheckerSeparationInvariant*");
    }

    [Fact]
    public void RemittanceInstrument_WalkInOverFiftyThousand_WithoutCdd_ThrowsDueDiligenceRequired()
    {
        // 50,000 BDT or above without customer CIF strictly requires walk-in CDD
        var action = () => new RemittanceInstrument(
            Guid.NewGuid(),
            "PO-0010003",
            RemittanceInstrumentType.PayOrder,
            "BR001",
            Money.Bdt(50000m),
            "Beneficiary Co.",
            purchaserCif: null,
            walkInDetails: null,
            makerUserId: "MAKER_01",
            checkerUserId: "CHECKER_01",
            issueDate: new DateOnly(2026, 11, 9));

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*WalkInDueDiligenceRequiredInvariant*");
    }

    [Fact]
    public void RemittanceInstrument_WalkInOverFiftyThousand_WithValidCdd_Succeeds()
    {
        var nid = new NationalIdentityNumber("1234567890");
        var cdd = new WalkInCustomerDetails("Mohammad Ali", nid, "01712345678", "Utility Bill Payment");

        var instrument = new RemittanceInstrument(
            Guid.NewGuid(),
            "PO-0010004",
            RemittanceInstrumentType.PayOrder,
            "BR001",
            Money.Bdt(75000m),
            "Titas Gas T&D Co.",
            purchaserCif: null,
            walkInDetails: cdd,
            makerUserId: "MAKER_01",
            checkerUserId: "CHECKER_01",
            issueDate: new DateOnly(2026, 11, 9));

        instrument.WalkInDetails.Should().NotBeNull();
        instrument.WalkInDetails!.FullName.Should().Be("Mohammad Ali");
        instrument.Status.Should().Be(RemittanceInstrumentStatus.Issued);
    }

    [Fact]
    public void RemittanceInstrument_Liquidate_ValidIssuedInstrument_TransitionsToLiquidated()
    {
        var instrument = CreateIssuedInstrument("PO-0010005", 25000m);

        instrument.Liquidate("BR002", "TELLER_02", new DateOnly(2026, 11, 10));

        instrument.Status.Should().Be(RemittanceInstrumentStatus.Liquidated);
        instrument.LiquidatedDate.Should().Be(new DateOnly(2026, 11, 10));
        instrument.LiquidatedBranchCode.Should().Be("BR002");
    }

    [Fact]
    public void RemittanceInstrument_Liquidate_AlreadyLiquidated_ThrowsDuplicateLiquidationBlocked()
    {
        var instrument = CreateIssuedInstrument("PO-0010006", 25000m);
        instrument.Liquidate("BR002", "TELLER_02", new DateOnly(2026, 11, 10));

        var action = () => instrument.Liquidate("BR001", "TELLER_01", new DateOnly(2026, 11, 11));

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*DuplicateLiquidationBlockedInvariant*");
    }

    [Fact]
    public void RemittanceInstrument_Liquidate_StoppedInstrument_ThrowsStoppedInstrumentBlocked()
    {
        var instrument = CreateIssuedInstrument("PO-0010007", 25000m);
        instrument.StopPayment("Customer lost instrument - GD #54321 filed", "SUPERVISOR_01");

        var action = () => instrument.Liquidate("BR002", "TELLER_02", new DateOnly(2026, 11, 10));

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*StoppedInstrumentLiquidationBlockedInvariant*");
    }

    [Fact]
    public void RemittanceInstrument_CancelAndRefund_RefundsOnlyPrincipal()
    {
        var instrument = CreateIssuedInstrument("PO-0010008", 25000m); // 25k principal + 25 comm + 3.75 VAT

        var refund = instrument.CancelAndRefund("Purchaser cancelled transaction", "SUPERVISOR_01");

        instrument.Status.Should().Be(RemittanceInstrumentStatus.Cancelled);
        refund.Should().Be(Money.Bdt(25000m)); // Only principal refunded, statutory fees retained
    }

    [Fact]
    public void RemittanceInstrument_IssueDuplicate_OnStoppedInstrument_Succeeds()
    {
        var instrument = CreateIssuedInstrument("PO-0010009", 25000m);
        instrument.StopPayment("Lost in transit", "SUPERVISOR_01");

        instrument.IssueDuplicate("PO-0010099", "INDEMNITY-BOND-REF-8877", "SUPERVISOR_01");

        instrument.Status.Should().Be(RemittanceInstrumentStatus.DuplicateIssued);
        instrument.DuplicateInstrumentNumber.Should().Be("PO-0010099");
        instrument.IndemnityBondReference.Should().Be("INDEMNITY-BOND-REF-8877");
    }

    [Fact]
    public void SecurityStationery_JointCustody_DistinctCustodianAndManager_AllocatesSequentialLeaves()
    {
        var stationery = new SecurityStationeryInventory(
            Guid.NewGuid(),
            "BR001",
            RemittanceInstrumentType.PayOrder,
            "PO",
            1001,
            1003,
            managerUserId: "MANAGER_01",
            custodianUserId: "CUSTODIAN_01");

        var leaf1 = stationery.AllocateNextLeaf();
        var leaf2 = stationery.AllocateNextLeaf();
        var leaf3 = stationery.AllocateNextLeaf();

        leaf1.Should().Be("PO-0001001");
        leaf2.Should().Be("PO-0001002");
        leaf3.Should().Be("PO-0001003");

        // Next allocation exceeds book
        var action = () => stationery.AllocateNextLeaf();
        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*StationeryExhaustedInvariant*");
    }

    [Fact]
    public void SecurityStationery_JointCustody_SameUser_ThrowsJointCustodySegregation()
    {
        var action = () => new SecurityStationeryInventory(
            Guid.NewGuid(),
            "BR001",
            RemittanceInstrumentType.PayOrder,
            "PO",
            1001,
            1050,
            managerUserId: "OFFICER_01",
            custodianUserId: "OFFICER_01");

        action.Should().Throw<InvariantViolationException>()
            .WithMessage("*JointCustodySegregationInvariant*");
    }

    private static RemittanceInstrument CreateIssuedInstrument(string number, decimal amount)
    {
        return new RemittanceInstrument(
            Guid.NewGuid(),
            number,
            RemittanceInstrumentType.PayOrder,
            "BR001",
            Money.Bdt(amount),
            "Beneficiary ABC",
            "CIF11223344",
            walkInDetails: null,
            makerUserId: "MAKER_01",
            checkerUserId: "CHECKER_01",
            issueDate: new DateOnly(2026, 11, 9));
    }
}
