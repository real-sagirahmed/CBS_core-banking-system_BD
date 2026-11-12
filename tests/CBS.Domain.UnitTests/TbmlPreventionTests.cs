using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Automated unit tests locking TBML & UNSCR Sanctions Prevention Engine invariants
// conforming to Guidelines for Prevention of Trade Based Money Laundering and Bangladesh Bank BRPD Circular No. 12 (Module 5.7).
public class TbmlPreventionTests
{
    [Fact]
    public void TbmlPriceBenchmark_OverInvoicing_Above20Percent_DetectsAlert()
    {
        // Arrange
        // Raw Cotton (HS Code 5201.00): Benchmark 2.00 to 2.50 USD/KG.
        var benchmark = new TbmlPriceBenchmark(
            Guid.NewGuid(),
            "5201.00",
            "Raw Cotton",
            "KG",
            2.00m,
            2.50m,
            Currency.USD,
            new DateOnly(2026, 1, 1),
            "ICAC Monthly Cotton Price Index");

        // Act - Import at 3.25 USD/KG (+30% above ceiling of 2.50)
        bool triggered = benchmark.CheckPriceVariance(
            3.25m,
            isExport: false,
            out decimal variancePercentage,
            out var alertType,
            out string? narrative);

        // Assert
        triggered.Should().BeTrue();
        alertType.Should().Be(TbmlAlertType.PriceVarianceOverInvoicing);
        variancePercentage.Should().Be(30.00m);
        narrative.Should().Contain("exceeds benchmark ceiling");
        narrative.Should().Contain("Suspected capital flight");
    }

    [Fact]
    public void TbmlPriceBenchmark_UnderInvoicing_Below20Percent_DetectsAlert()
    {
        // Arrange
        // Men's T-Shirts (HS Code 6109.10): Benchmark 4.00 to 8.00 USD/PC.
        var benchmark = new TbmlPriceBenchmark(
            Guid.NewGuid(),
            "6109.10",
            "Men's Cotton T-Shirts",
            "PC",
            4.00m,
            8.00m,
            Currency.USD,
            new DateOnly(2026, 1, 1),
            "BGMEA Minimum Export Benchmark");

        // Act - Export at 2.60 USD/PC (-35% below floor of 4.00)
        bool triggered = benchmark.CheckPriceVariance(
            2.60m,
            isExport: true,
            out decimal variancePercentage,
            out var alertType,
            out string? narrative);

        // Assert
        triggered.Should().BeTrue();
        alertType.Should().Be(TbmlAlertType.PriceVarianceUnderInvoicing);
        variancePercentage.Should().Be(-35.00m);
        narrative.Should().Contain("is below benchmark floor");
        narrative.Should().Contain("Suspected illicit funds retention");
    }

    [Fact]
    public void TbmlPriceBenchmark_WithinTolerance_ReturnsFalse()
    {
        // Arrange
        var benchmark = new TbmlPriceBenchmark(
            Guid.NewGuid(),
            "5201.00",
            "Raw Cotton",
            "KG",
            2.00m,
            2.50m,
            Currency.USD,
            new DateOnly(2026, 1, 1),
            "ICAC Monthly Index");

        // Act - Declared price 2.60 USD (+4% above max, <= 20% threshold)
        bool triggered = benchmark.CheckPriceVariance(
            2.60m,
            isExport: false,
            out decimal variancePercentage,
            out var alertType,
            out string? narrative);

        // Assert
        triggered.Should().BeFalse();
        alertType.Should().BeNull();
        narrative.Should().BeNull();
    }

    [Fact]
    public void TradeTransactionProfile_EvaluateTransaction_DetectsVolumeBreach()
    {
        // Arrange
        var ttp = new TradeTransactionProfile(
            Guid.NewGuid(),
            new CifNumber("CIF-TRADER-001"),
            Money.Usd(1_000_000m),
            Money.Usd(2_000_000m),
            "US,GB,DE",
            "6109,6203",
            "Medium",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            "CREDIT-OFFICER-01");

        // Act - Try importing 1,200,000 USD (limit is 1,000,000 USD)
        var anomalies = ttp.EvaluateTransaction(Money.Usd(1_200_000m), "DE", "6109.10", isExport: false);

        // Assert
        anomalies.Should().Contain(a => a.Contains("exceeds customer approved TTP limit"));
    }

    [Fact]
    public void TradeTransactionProfile_EvaluateTransaction_DetectsUnapprovedCountry()
    {
        // Arrange
        var ttp = new TradeTransactionProfile(
            Guid.NewGuid(),
            new CifNumber("CIF-TRADER-001"),
            Money.Usd(1_000_000m),
            Money.Usd(2_000_000m),
            "US,GB,DE",
            "6109,6203",
            "Medium",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            "CREDIT-OFFICER-01");

        // Act - Import from Cayman Islands (KY)
        var anomalies = ttp.EvaluateTransaction(Money.Usd(100_000m), "KY", "6109.10", isExport: false);

        // Assert
        anomalies.Should().Contain(a => a.Contains("is not in customer's approved TTP permitted trading partner countries"));
    }

    [Fact]
    public void TradeTransactionProfile_EvaluateTransaction_DetectsMismatchedCommodity()
    {
        // Arrange
        var ttp = new TradeTransactionProfile(
            Guid.NewGuid(),
            new CifNumber("CIF-TRADER-001"),
            Money.Usd(1_000_000m),
            Money.Usd(2_000_000m),
            "US,GB,DE",
            "6109,6203", // Garments
            "Medium",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            "CREDIT-OFFICER-01");

        // Act - Trade in IT equipment (HS Code 8471.30)
        var anomalies = ttp.EvaluateTransaction(Money.Usd(50_000m), "US", "8471.30", isExport: false);

        // Assert
        anomalies.Should().Contain(a => a.Contains("does not match customer's approved TTP trade goods line"));
    }

    [Fact]
    public void TbmlAlertRecord_CriticalSeverity_AutoEscalatesToCamlcoWithStrReference()
    {
        // Arrange & Act
        var alert = new TbmlAlertRecord(
            Guid.NewGuid(),
            "LC-IMP-2026-9999",
            new CifNumber("CIF-TRADER-001"),
            TbmlAlertType.UnscrSanctionHit,
            TbmlSeverity.Critical,
            "Trade counterparty located in high-risk jurisdiction under UNSCR sanctions.");

        // Assert
        alert.Severity.Should().Be(TbmlSeverity.Critical);
        alert.IsEscalatedToCamlco.Should().BeTrue();
        alert.StrSarReference.Should().NotBeNullOrWhiteSpace();
        alert.StrSarReference.Should().StartWith("BFIU-STR-AUTO-");
    }

    [Fact]
    public void TbmlAlertRecord_ManualEscalation_SetsStatusAndReviewer()
    {
        // Arrange
        var alert = new TbmlAlertRecord(
            Guid.NewGuid(),
            "LC-IMP-2026-8888",
            new CifNumber("CIF-TRADER-002"),
            TbmlAlertType.RoundFigureHighValueAnomaly,
            TbmlSeverity.High,
            "Repetitive high-value round transaction detected ($500,000.00).");

        // Act
        alert.EscalateToCamlco("AML-OFFICER-01", "BFIU-STR-MANUAL-2026-001", "Escalating suspicious structuring pattern to BFIU.");

        // Assert
        alert.IsEscalatedToCamlco.Should().BeTrue();
        alert.Status.Should().Be(TbmlAlertStatus.EscalatedToCamlco);
        alert.StrSarReference.Should().Be("BFIU-STR-MANUAL-2026-001");
        alert.ReviewedByUserId.Should().Be("AML-OFFICER-01");
        alert.ReviewRemarks.Should().Contain("Escalating suspicious structuring pattern");
    }

    [Fact]
    public void TbmlAlertRecord_ClearWithJustification_UpdatesStatus()
    {
        // Arrange
        var alert = new TbmlAlertRecord(
            Guid.NewGuid(),
            "LC-IMP-2026-7777",
            new CifNumber("CIF-TRADER-003"),
            TbmlAlertType.PriceVarianceOverInvoicing,
            TbmlSeverity.Medium,
            "Declared price 22% higher than standard index.");

        // Act
        alert.ClearAlert("COMPLIANCE-HEAD", "Verified premium organic certified Egyptian cotton with verified laboratory testing.");

        // Assert
        alert.Status.Should().Be(TbmlAlertStatus.ClearedWithJustification);
        alert.ReviewedByUserId.Should().Be("COMPLIANCE-HEAD");
        alert.ReviewRemarks.Should().Contain("premium organic certified");
    }

    [Fact]
    public void VesselTrackingRecord_Creation_RecordsAuditTrail()
    {
        // Arrange & Act
        var vessel = new VesselTrackingRecord(
            Guid.NewGuid(),
            "MV BANGLAR SHOURABH",
            "9123456",
            "BD",
            "PORT OF SINGAPORE",
            "CHITTAGONG PORT",
            isSanctionedOrBlacklisted: false,
            "LOGISTICS-OFFICER-01");

        // Assert
        vessel.VesselName.Should().Be("MV BANGLAR SHOURABH");
        vessel.ImoNumber.Should().Be("9123456");
        vessel.FlagCountry.Should().Be("BD");
        vessel.IsSanctionedOrBlacklisted.Should().BeFalse();
        vessel.LoggedByUserId.Should().Be("LOGISTICS-OFFICER-01");
    }
}
