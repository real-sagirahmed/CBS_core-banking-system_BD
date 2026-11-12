using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Automated unit tests locking Master Export LC & B2B allocation invariants
// strictly conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 5.2.2-i, 5.5.1, and 5.6.1.
public class MasterExportLcTests
{
    private static readonly DateOnly BaseIssueDate = new(2026, 1, 15);
    private static readonly DateOnly BaseExpiryDate = new(2026, 7, 15);
    private static readonly DateOnly BaseShipmentDate = new(2026, 6, 30);

    [Fact]
    public void MasterExportLc_Creation_SetsDraftStatusAndCalculatesCeiling()
    {
        // Arrange & Act
        // Master LC: 1,000,000 USD, Local Value Addition: 25%
        // Max B2B Ceiling = 1,000,000 * (1 - 0.25) = 750,000 USD
        var masterLc = new MasterExportLc(
            "MLC-2026-EXP-001",
            MasterContractType.ExportLetterOfCredit,
            new CifNumber("CIF-EXP-001"),
            "HSBCBDDH",
            "HSBC Bank plc London",
            "Marks & Spencer plc",
            "GB",
            Money.Usd(1_000_000m),
            25.00m,
            BaseIssueDate,
            BaseExpiryDate,
            BaseShipmentDate,
            "0101",
            "MAKER-01");

        // Assert
        masterLc.MasterLcNumber.Should().Be("MLC-2026-EXP-001");
        masterLc.ContractType.Should().Be(MasterContractType.ExportLetterOfCredit);
        masterLc.Status.Should().Be(MasterLcStatus.Draft);
        masterLc.ValueAdditionPercentage.Should().Be(25.00m);
        masterLc.MaxPermissibleB2bAmount.Amount.Should().Be(750_000m);
        masterLc.CumulativeB2bAllocatedAmount.Amount.Should().Be(0m);
        masterLc.RemainingB2bCapacity.Amount.Should().Be(750_000m);
    }

    [Fact]
    public void MasterExportLc_Authorize_DualControl_Succeeds()
    {
        // Arrange
        var masterLc = CreateSampleMasterLc();

        // Act
        masterLc.Authorize("CHECKER-02");

        // Assert
        masterLc.Status.Should().Be(MasterLcStatus.Active);
        masterLc.CheckerUserId.Should().Be("CHECKER-02");
        masterLc.AuthorizedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MasterExportLc_Authorize_MakerCheckerSeparation_Violation_ThrowsInvariantViolation()
    {
        // Arrange
        var masterLc = CreateSampleMasterLc();

        // Act & Assert
        var act = () => masterLc.Authorize("MAKER-01");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Maker cannot act as Checker*")
            .Where(e => e.InvariantName == "MakerCheckerSeparationInvariant");
    }

    [Fact]
    public void MasterExportLc_AllocateB2bLimit_WithinCeiling_Succeeds()
    {
        // Arrange
        var masterLc = CreateSampleMasterLc();
        masterLc.Authorize("CHECKER-02");

        var b2bShipment = new DateOnly(2026, 5, 31);
        var b2bExpiry = new DateOnly(2026, 6, 15);
        var asOfDate = new DateOnly(2026, 2, 1);

        // Act - Allocate 400,000 USD (ceiling is 750,000 USD)
        masterLc.AllocateB2bLimit(Money.Usd(400_000m), b2bShipment, b2bExpiry, asOfDate);

        // Assert
        masterLc.CumulativeB2bAllocatedAmount.Amount.Should().Be(400_000m);
        masterLc.RemainingB2bCapacity.Amount.Should().Be(350_000m);
    }

    [Fact]
    public void MasterExportLc_AllocateB2bLimit_ExceedingCeiling_ThrowsInvariantViolation()
    {
        // Arrange
        var masterLc = CreateSampleMasterLc();
        masterLc.Authorize("CHECKER-02");

        var b2bShipment = new DateOnly(2026, 5, 31);
        var b2bExpiry = new DateOnly(2026, 6, 15);
        var asOfDate = new DateOnly(2026, 2, 1);

        // Act - Try allocating 800,000 USD (exceeds 750,000 USD ceiling)
        var act = () => masterLc.AllocateB2bLimit(Money.Usd(800_000m), b2bShipment, b2bExpiry, asOfDate);

        // Assert
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*exceeds statutory ceiling of 750000.00 USD*")
            .Where(e => e.InvariantName == "B2bValueAdditionCeilingInvariant");
    }

    [Fact]
    public void MasterExportLc_AllocateB2bLimit_WhenB2bExpiryDateNotEarlierThanMasterExpiry_ThrowsInvariantViolation()
    {
        // Arrange
        var masterLc = CreateSampleMasterLc();
        masterLc.Authorize("CHECKER-02");

        var b2bShipment = new DateOnly(2026, 5, 31);
        var b2bExpiryViolating = BaseExpiryDate; // Same as Master LC expiry
        var asOfDate = new DateOnly(2026, 2, 1);

        // Act & Assert
        var act = () => masterLc.AllocateB2bLimit(Money.Usd(200_000m), b2bShipment, b2bExpiryViolating, asOfDate);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*must be strictly earlier than Master LC expiry date*")
            .Where(e => e.InvariantName == "B2bExpiryDateInvariant");
    }

    [Fact]
    public void MasterExportLc_AllocateB2bLimit_WhenB2bShipmentDateNotEarlierThanMasterShipment_ThrowsInvariantViolation()
    {
        // Arrange
        var masterLc = CreateSampleMasterLc();
        masterLc.Authorize("CHECKER-02");

        var b2bShipmentViolating = BaseShipmentDate; // Same as Master LC shipment date
        var b2bExpiry = new DateOnly(2026, 6, 15);
        var asOfDate = new DateOnly(2026, 2, 1);

        // Act & Assert
        var act = () => masterLc.AllocateB2bLimit(Money.Usd(200_000m), b2bShipmentViolating, b2bExpiry, asOfDate);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*must be strictly earlier than Master LC shipment date*")
            .Where(e => e.InvariantName == "B2bShipmentDateInvariant");
    }

    [Fact]
    public void MasterExportLc_AllocateB2bLimit_WhenMasterLcExpired_ThrowsInvariantViolation()
    {
        // Arrange
        var masterLc = CreateSampleMasterLc();
        masterLc.Authorize("CHECKER-02");

        var b2bShipment = new DateOnly(2026, 5, 31);
        var b2bExpiry = new DateOnly(2026, 6, 15);
        var postExpiryDate = BaseExpiryDate.AddDays(1);

        // Act & Assert
        var act = () => masterLc.AllocateB2bLimit(Money.Usd(200_000m), b2bShipment, b2bExpiry, postExpiryDate);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*has expired*Back-to-Back LC issuance strictly prohibited*")
            .Where(e => e.InvariantName == "MasterLcExpiredInvariant");
    }

    [Fact]
    public void MasterExportLc_AmendAmount_DecreasingBelowActiveB2bAllocations_ThrowsInvariantViolation()
    {
        // Arrange
        var masterLc = CreateSampleMasterLc();
        masterLc.Authorize("CHECKER-02");

        // Allocate 500,000 USD
        masterLc.AllocateB2bLimit(Money.Usd(500_000m), new DateOnly(2026, 5, 31), new DateOnly(2026, 6, 15), new DateOnly(2026, 2, 1));

        // Act - Attempt to decrease Master LC to 600,000 USD (New ceiling would be 600,000 * 0.75 = 450,000 USD < 500,000 USD active)
        var act = () => masterLc.AmendAmount(Money.Usd(600_000m), "MAKER-01", "CHECKER-02");

        // Assert
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*New B2B ceiling (450000.00) is less than active B2B allocation (500000)*")
            .Where(e => e.InvariantName == "MasterLcValueDecreaseAmendmentInvariant");
    }

    [Fact]
    public void MasterExportLc_AmendAmount_MakerCheckerSeparation_ThrowsInvariantViolation()
    {
        // Arrange
        var masterLc = CreateSampleMasterLc();
        masterLc.Authorize("CHECKER-02");

        // Act & Assert
        var act = () => masterLc.AmendAmount(Money.Usd(1_200_000m), "MAKER-01", "MAKER-01");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Maker cannot act as Checker*")
            .Where(e => e.InvariantName == "MakerCheckerSeparationInvariant");
    }

    [Fact]
    public void MasterExportLc_Close_WithActiveB2b_ThrowsInvariantViolation()
    {
        // Arrange
        var masterLc = CreateSampleMasterLc();
        masterLc.Authorize("CHECKER-02");

        // Allocate 300,000 USD
        masterLc.AllocateB2bLimit(Money.Usd(300_000m), new DateOnly(2026, 5, 31), new DateOnly(2026, 6, 15), new DateOnly(2026, 2, 1));

        // Act & Assert
        var act = () => masterLc.Close("MAKER-01", "CHECKER-02");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*active Back-to-Back LCs totaling * USD remain open*")
            .Where(e => e.InvariantName == "MasterLcCannotCloseWithActiveB2bInvariant");
    }

    [Fact]
    public void MasterExportLc_ReleaseB2bLimit_AndClose_Succeeds()
    {
        // Arrange
        var masterLc = CreateSampleMasterLc();
        masterLc.Authorize("CHECKER-02");

        // Allocate 300,000 USD
        masterLc.AllocateB2bLimit(Money.Usd(300_000m), new DateOnly(2026, 5, 31), new DateOnly(2026, 6, 15), new DateOnly(2026, 2, 1));

        // Release 300,000 USD
        masterLc.ReleaseB2bLimit(Money.Usd(300_000m));
        masterLc.CumulativeB2bAllocatedAmount.Amount.Should().Be(0m);

        // Act - Close
        masterLc.Close("MAKER-01", "CHECKER-02");

        // Assert
        masterLc.Status.Should().Be(MasterLcStatus.Closed);
    }

    private static MasterExportLc CreateSampleMasterLc()
    {
        return new MasterExportLc(
            "MLC-2026-EXP-SAMPLE",
            MasterContractType.ExportLetterOfCredit,
            new CifNumber("CIF-EXP-001"),
            "HSBCBDDH",
            "HSBC Bank plc London",
            "Marks & Spencer plc",
            "GB",
            Money.Usd(1_000_000m),
            25.00m,
            BaseIssueDate,
            BaseExpiryDate,
            BaseShipmentDate,
            "0101",
            "MAKER-01");
    }
}
