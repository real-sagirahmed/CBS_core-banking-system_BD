using CBS.Domain.Enums;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class TradeFinanceTests
{
    private readonly Guid _validApplicantId = Guid.NewGuid();

    [Fact]
    public void CreateDraft_WhenCibDefaulter_ShouldThrowRegulatoryInvariantViolation()
    {
        // Arrange
        var tbml = TbmlScreeningResult.Evaluate(false, 100m, 90m, 110m, "CHATTOGRAM", "SINGAPORE", false);
        var hsCode = HsCode.Create("5201.00.00");

        // Act
        var act = () => DocumentaryCredit.CreateDraft(
            lcNumber: "LC-2026-001-0001",
            type: LcType.ImportSight,
            applicantId: _validApplicantId,
            beneficiaryName: "Global Cotton Inc",
            beneficiaryAddress: "Cotton Way, Memphis, USA",
            beneficiaryCountry: "USA",
            advisingBankBic: "CHASUS33",
            advisingBankName: "JPMorgan Chase Bank",
            amount: new Money(50000m, Currency.USD),
            cashMarginAmount: new Money(10000m, Currency.USD),
            tolerancePercentage: 5m,
            issueDate: new DateTime(2026, 5, 1),
            expiryDate: new DateTime(2026, 8, 1),
            expiryPlace: "BANGLADESH",
            latestShipmentDate: new DateTime(2026, 7, 15),
            portOfLoading: "SAVANNAH",
            portOfDischarge: "CHATTOGRAM",
            goodsDescription: "Raw Cotton",
            hsCode: hsCode,
            incoterm: Incoterm.CFR,
            ircNumber: "IRC-998877",
            isIrcValid: true,
            isCibDefaulter: true, // Defaulter hit!
            tbmlResult: tbml,
            makerUserId: "OFFICER_01");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*defaulter borrower*");
    }

    [Fact]
    public void CreateDraft_WhenIrcInvalid_ShouldThrowRegulatoryInvariantViolation()
    {
        // Arrange
        var tbml = TbmlScreeningResult.Evaluate(false, 100m, 90m, 110m, "CHATTOGRAM", "SINGAPORE", false);
        var hsCode = HsCode.Create("5201.00.00");

        // Act
        var act = () => DocumentaryCredit.CreateDraft(
            lcNumber: "LC-2026-001-0001",
            type: LcType.ImportSight,
            applicantId: _validApplicantId,
            beneficiaryName: "Global Cotton Inc",
            beneficiaryAddress: "Cotton Way, Memphis, USA",
            beneficiaryCountry: "USA",
            advisingBankBic: "CHASUS33",
            advisingBankName: "JPMorgan Chase Bank",
            amount: new Money(50000m, Currency.USD),
            cashMarginAmount: new Money(10000m, Currency.USD),
            tolerancePercentage: 5m,
            issueDate: new DateTime(2026, 5, 1),
            expiryDate: new DateTime(2026, 8, 1),
            expiryPlace: "BANGLADESH",
            latestShipmentDate: new DateTime(2026, 7, 15),
            portOfLoading: "SAVANNAH",
            portOfDischarge: "CHATTOGRAM",
            goodsDescription: "Raw Cotton",
            hsCode: hsCode,
            incoterm: Incoterm.CFR,
            ircNumber: "IRC-EXPIRED",
            isIrcValid: false, // Invalid IRC
            isCibDefaulter: false,
            tbmlResult: tbml,
            makerUserId: "OFFICER_01");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Import Registration Certificate (IRC)*");
    }

    [Fact]
    public void CreateDraft_WhenSanctionHit_ShouldThrowRegulatoryInvariantViolation()
    {
        // Arrange
        var tbml = TbmlScreeningResult.Evaluate(true, 100m, 90m, 110m, "CHATTOGRAM", "SINGAPORE", false); // Sanction Hit
        var hsCode = HsCode.Create("5201.00.00");

        // Act
        var act = () => DocumentaryCredit.CreateDraft(
            lcNumber: "LC-2026-001-0001",
            type: LcType.ImportSight,
            applicantId: _validApplicantId,
            beneficiaryName: "Sanctioned Entity LLC",
            beneficiaryAddress: "Tehran, Iran",
            beneficiaryCountry: "IRAN",
            advisingBankBic: "MELIIRTH",
            advisingBankName: "Bank Melli Iran",
            amount: new Money(50000m, Currency.USD),
            cashMarginAmount: new Money(10000m, Currency.USD),
            tolerancePercentage: 5m,
            issueDate: new DateTime(2026, 5, 1),
            expiryDate: new DateTime(2026, 8, 1),
            expiryPlace: "BANGLADESH",
            latestShipmentDate: new DateTime(2026, 7, 15),
            portOfLoading: "BANDAR ABBAS",
            portOfDischarge: "CHATTOGRAM",
            goodsDescription: "Chemicals",
            hsCode: hsCode,
            incoterm: Incoterm.CFR,
            ircNumber: "IRC-998877",
            isIrcValid: true,
            isCibDefaulter: false,
            tbmlResult: tbml,
            makerUserId: "OFFICER_01");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Sanction screening hit detected*");
    }

    [Fact]
    public void CreateDraft_BackToBack_WhenAmountExceeds75PercentCeiling_ShouldThrowViolation()
    {
        // Arrange
        var tbml = TbmlScreeningResult.Evaluate(false, 100m, 90m, 110m, "CHATTOGRAM", "SINGAPORE", false);
        var hsCode = HsCode.Create("5208.11.00");
        var masterLcAmount = new Money(100000m, Currency.USD);
        var proposedB2BAmount = new Money(80000m, Currency.USD); // 80% > 75% statutory ceiling!

        // Act
        var act = () => DocumentaryCredit.CreateDraft(
            lcNumber: "B2B-2026-001-0001",
            type: LcType.BackToBack,
            applicantId: _validApplicantId,
            beneficiaryName: "Fabrics Supplier Ltd",
            beneficiaryAddress: "Shanghai, China",
            beneficiaryCountry: "CHINA",
            advisingBankBic: "BKCHCNBJ",
            advisingBankName: "Bank of China",
            amount: proposedB2BAmount,
            cashMarginAmount: new Money(0m, Currency.USD),
            tolerancePercentage: 5m,
            issueDate: new DateTime(2026, 5, 1),
            expiryDate: new DateTime(2026, 7, 1),
            expiryPlace: "BANGLADESH",
            latestShipmentDate: new DateTime(2026, 6, 15),
            portOfLoading: "SHANGHAI",
            portOfDischarge: "CHATTOGRAM",
            goodsDescription: "Woven Fabrics for Export RMG",
            hsCode: hsCode,
            incoterm: Incoterm.FOB,
            ircNumber: "IRC-998877",
            isIrcValid: true,
            isCibDefaulter: false,
            tbmlResult: tbml,
            makerUserId: "OFFICER_01",
            masterLcNumber: "MLC-2026-EXP-009",
            masterLcAmount: masterLcAmount,
            masterLcShipmentDate: new DateTime(2026, 8, 1),
            masterLcExpiryDate: new DateTime(2026, 9, 1));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*exceeds statutory ceiling of 75%*");
    }

    [Fact]
    public void CreateDraft_BackToBack_WhenShipmentDateNotEarlierThanMasterLc_ShouldThrowViolation()
    {
        // Arrange
        var tbml = TbmlScreeningResult.Evaluate(false, 100m, 90m, 110m, "CHATTOGRAM", "SINGAPORE", false);
        var hsCode = HsCode.Create("5208.11.00");
        var masterLcAmount = new Money(100000m, Currency.USD);
        var proposedB2BAmount = new Money(70000m, Currency.USD); // 70% <= 75%

        // Act: B2B Shipment Date is after Master LC Shipment Date
        var act = () => DocumentaryCredit.CreateDraft(
            lcNumber: "B2B-2026-001-0001",
            type: LcType.BackToBack,
            applicantId: _validApplicantId,
            beneficiaryName: "Fabrics Supplier Ltd",
            beneficiaryAddress: "Shanghai, China",
            beneficiaryCountry: "CHINA",
            advisingBankBic: "BKCHCNBJ",
            advisingBankName: "Bank of China",
            amount: proposedB2BAmount,
            cashMarginAmount: new Money(0m, Currency.USD),
            tolerancePercentage: 5m,
            issueDate: new DateTime(2026, 5, 1),
            expiryDate: new DateTime(2026, 8, 15),
            expiryPlace: "BANGLADESH",
            latestShipmentDate: new DateTime(2026, 8, 10), // Later than master shipment!
            portOfLoading: "SHANGHAI",
            portOfDischarge: "CHATTOGRAM",
            goodsDescription: "Woven Fabrics for Export RMG",
            hsCode: hsCode,
            incoterm: Incoterm.FOB,
            ircNumber: "IRC-998877",
            isIrcValid: true,
            isCibDefaulter: false,
            tbmlResult: tbml,
            makerUserId: "OFFICER_01",
            masterLcNumber: "MLC-2026-EXP-009",
            masterLcAmount: masterLcAmount,
            masterLcShipmentDate: new DateTime(2026, 8, 1), // Master shipment
            masterLcExpiryDate: new DateTime(2026, 9, 1));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Shipment date of Back-to-Back LC must be strictly earlier*");
    }

    [Fact]
    public void AuthorizeAndIssue_WhenMakerEqualsChecker_ShouldThrowDualControlViolation()
    {
        // Arrange
        var tbml = TbmlScreeningResult.Evaluate(false, 100m, 90m, 110m, "CHATTOGRAM", "SINGAPORE", false);
        var hsCode = HsCode.Create("5201.00.00");
        var lc = DocumentaryCredit.CreateDraft(
            lcNumber: "LC-2026-001-0001",
            type: LcType.ImportSight,
            applicantId: _validApplicantId,
            beneficiaryName: "Global Cotton Inc",
            beneficiaryAddress: "Cotton Way, Memphis, USA",
            beneficiaryCountry: "USA",
            advisingBankBic: "CHASUS33",
            advisingBankName: "JPMorgan Chase Bank",
            amount: new Money(50000m, Currency.USD),
            cashMarginAmount: new Money(10000m, Currency.USD),
            tolerancePercentage: 5m,
            issueDate: new DateTime(2026, 5, 1),
            expiryDate: new DateTime(2026, 8, 1),
            expiryPlace: "BANGLADESH",
            latestShipmentDate: new DateTime(2026, 7, 15),
            portOfLoading: "SAVANNAH",
            portOfDischarge: "CHATTOGRAM",
            goodsDescription: "Raw Cotton",
            hsCode: hsCode,
            incoterm: Incoterm.CFR,
            ircNumber: "IRC-998877",
            isIrcValid: true,
            isCibDefaulter: false,
            tbmlResult: tbml,
            makerUserId: "OFFICER_01");

        // Act & Assert
        var act = () => lc.AuthorizeAndIssue("OFFICER_01");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Dual-control separation of duty enforced*");
    }

    [Fact]
    public void AuthorizeAndIssue_ValidChecker_ShouldGenerateValidSwiftMt700Message()
    {
        // Arrange
        var tbml = TbmlScreeningResult.Evaluate(false, 100m, 90m, 110m, "CHATTOGRAM", "SINGAPORE", false);
        var hsCode = HsCode.Create("7207.11.00"); // Iron/Steel billet, requires PSI!
        var lc = DocumentaryCredit.CreateDraft(
            lcNumber: "LC-2026-001-0001",
            type: LcType.ImportSight,
            applicantId: _validApplicantId,
            beneficiaryName: "ArcelorMittal Steel",
            beneficiaryAddress: "Luxembourg City, Luxembourg",
            beneficiaryCountry: "LUXEMBOURG",
            advisingBankBic: "BCEELULL",
            advisingBankName: "Banque et Caisse d Epargne de l Etat",
            amount: new Money(250000m, Currency.USD),
            cashMarginAmount: new Money(50000m, Currency.USD),
            tolerancePercentage: 5m,
            issueDate: new DateTime(2026, 5, 1),
            expiryDate: new DateTime(2026, 8, 1),
            expiryPlace: "BANGLADESH",
            latestShipmentDate: new DateTime(2026, 7, 15),
            portOfLoading: "ROTTERDAM",
            portOfDischarge: "CHATTOGRAM",
            goodsDescription: "Steel Billets",
            hsCode: hsCode,
            incoterm: Incoterm.CFR,
            ircNumber: "IRC-998877",
            isIrcValid: true,
            isCibDefaulter: false,
            tbmlResult: tbml,
            makerUserId: "MAKER_OFFICER");

        // Act
        lc.AuthorizeAndIssue("CHECKER_SUPERVISOR");

        // Assert
        lc.Status.Should().Be(LcStatus.Issued);
        lc.CheckerUserId.Should().Be("CHECKER_SUPERVISOR");
        lc.Mt700Message.Should().NotBeNull();
        lc.Mt700Message!.RawMessageText.Should().Contain(":40A:IRREVOCABLE");
        lc.Mt700Message.RawMessageText.Should().Contain(":20:LC-2026-001-0001");
        lc.Mt700Message.RawMessageText.Should().Contain(":32B:USD250000.00");
        lc.Mt700Message.RawMessageText.Should().Contain("PRE-SHIPMENT INSPECTION (PSI)");
    }

    [Fact]
    public void ShippingGuarantee_WhenMarginLessThan100Percent_ShouldThrowViolation()
    {
        // Act: Guarantee is 100,000 USD but Cash Margin is only 50,000 USD
        var act = () => ShippingGuarantee.Issue(
            sgReference: "SG-2026-001-0001",
            lcNumber: "LC-2026-001-0001",
            applicantId: _validApplicantId,
            carrierName: "Maersk Line",
            vesselName: "Maersk Mc-Kinney Moller",
            portOfDischarge: "CHATTOGRAM",
            guaranteeAmount: new Money(100000m, Currency.USD),
            cashMarginAmount: new Money(50000m, Currency.USD), // Only 50%
            makerUserId: "OFFICER_01");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Shipping Guarantee strictly requires 100% cash margin*");
    }

    [Fact]
    public void ShippingGuarantee_ReleaseWithoutBlNumber_ShouldThrowViolation()
    {
        // Arrange
        var sg = ShippingGuarantee.Issue(
            sgReference: "SG-2026-001-0001",
            lcNumber: "LC-2026-001-0001",
            applicantId: _validApplicantId,
            carrierName: "Maersk Line",
            vesselName: "Maersk Mc-Kinney Moller",
            portOfDischarge: "CHATTOGRAM",
            guaranteeAmount: new Money(100000m, Currency.USD),
            cashMarginAmount: new Money(100000m, Currency.USD),
            makerUserId: "OFFICER_01");

        sg.Authorize("SUPERVISOR_01");

        // Act & Assert
        var act = () => sg.ReleaseUponOriginalBlSurrender(string.Empty, "OFFICER_01");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Original Bill of Lading (BL) number is required*");
    }

    [Fact]
    public void ShippingGuarantee_ValidReleaseWithBlNumber_ShouldTransitionToReleased()
    {
        // Arrange
        var sg = ShippingGuarantee.Issue(
            sgReference: "SG-2026-001-0001",
            lcNumber: "LC-2026-001-0001",
            applicantId: _validApplicantId,
            carrierName: "Maersk Line",
            vesselName: "Maersk Mc-Kinney Moller",
            portOfDischarge: "CHATTOGRAM",
            guaranteeAmount: new Money(100000m, Currency.USD),
            cashMarginAmount: new Money(100000m, Currency.USD),
            makerUserId: "OFFICER_01");

        sg.Authorize("SUPERVISOR_01");

        // Act
        sg.ReleaseUponOriginalBlSurrender("MSK-BL-998877", "OFFICER_01");

        // Assert
        sg.Status.Should().Be(ShippingGuaranteeStatus.ReleasedUponBlSurrender);
        sg.SurrenderedOriginalBlNumber.Should().Be("MSK-BL-998877");
    }

    [Fact]
    public void ExpRegulatoryTracking_WhenProceedsNotRealizedWithin120Days_ShouldBeMarkedOverdue()
    {
        // Arrange: Shipment was on Jan 1, 2026 (120 days deadline is May 1, 2026)
        var shipmentDate = new DateTime(2026, 1, 1);
        var exp = ExpRegulatoryTracking.Register(
            expNumber: "00112026000001",
            exportLcOrContractNumber: "MLC-2026-EXP-001",
            exporterCustomerId: _validApplicantId,
            destinationCountry: "GERMANY",
            invoiceAmount: new Money(75000m, Currency.EUR),
            shipmentDate: shipmentDate,
            makerUserId: "OFFICER_01");

        exp.RealizationDeadlineDate.Should().Be(new DateTime(2026, 5, 1));
        exp.Status.Should().Be(ExpTrackingStatus.InTransit);

        // Act 1: Evaluation at 90 days (April 1, 2026)
        var isOverdueAt90Days = exp.EvaluateOverdueStatus(new DateTime(2026, 4, 1));
        isOverdueAt90Days.Should().BeFalse();
        exp.Status.Should().Be(ExpTrackingStatus.InTransit);

        // Act 2: Evaluation at 125 days (May 6, 2026)
        var isOverdueAt125Days = exp.EvaluateOverdueStatus(new DateTime(2026, 5, 6));

        // Assert
        isOverdueAt125Days.Should().BeTrue();
        exp.Status.Should().Be(ExpTrackingStatus.OverdueReportedToBangladeshBank);
        exp.ReportedToBangladeshBank.Should().BeTrue();
    }

    [Fact]
    public void TbmlScreening_OverInvoicingAndHighRiskPort_ShouldTriggerHighRiskAlert()
    {
        // Declared unit price 200 USD/kg vs benchmark max 100 USD/kg (+100% variance)
        // High risk port Bandar Abbas
        var result = TbmlScreeningResult.Evaluate(
            entitySanctionHit: false,
            declaredUnitPrice: 200m,
            benchmarkMinPrice: 80m,
            benchmarkMaxPrice: 100m,
            portOfLoading: "BANDAR ABBAS",
            portOfDischarge: "CHATTOGRAM",
            isDualUseHsCode: false);

        result.HasPriceVarianceAnomaly.Should().BeTrue();
        result.HasHighRiskPortRoute.Should().BeTrue();
        result.UnitPriceVariancePercentage.Should().Be(100m);
        result.RiskRating.Should().Be(TbmlRiskRating.High);
        result.RiskSummary.Should().Contain("Over-invoicing suspected");
        result.RiskSummary.Should().Contain("High-risk or sanctioned port");
    }

    [Fact]
    public void HsCode_RequiresPreShipmentInspection_ForChapter7207()
    {
        var hsCodeSteel = HsCode.Create("7207.11.00");
        hsCodeSteel.RequiresPreShipmentInspection.Should().BeTrue();

        var hsCodeCotton = HsCode.Create("5201.00.00");
        hsCodeCotton.RequiresPreShipmentInspection.Should().BeFalse();
    }
}
