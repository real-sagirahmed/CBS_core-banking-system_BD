using CBS.Domain.BranchCash;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Automated unit tests locking Security Deposit Receipt (SDR)
// and Remittance Stop Payment Revocation invariants conforming to BRPD Circular 12/2024 Modules 2.1.4, 2.1.1-ix, 2.1.2-vi.
public class SecurityDepositReceiptTests
{
    private static readonly DateOnly BaseIssueDate = new(2026, 2, 1);
    private static readonly DateOnly BaseExpiryDate = new(2026, 8, 1);

    [Fact]
    public void Sdr_Issuance_WithValidParameters_CalculatesTieredCommissionAnd15PercentVat()
    {
        // Arrange & Act (Amount: 150,000 BDT -> tier <= 200,000 is 100 BDT commission; VAT: 15 BDT)
        var sdr = new SecurityDepositReceipt(
            Guid.NewGuid(),
            "SDR-0101-2026-0001",
            "0101",
            Money.Bdt(150_000m),
            "RFP-PWD-DHAKA-2026-01",
            "Executive Engineer, PWD Dhaka Division-1",
            "CIF-CORP-001",
            null,
            BaseIssueDate,
            BaseExpiryDate,
            "TELLER-01",
            "CHECKER-02");

        // Assert
        sdr.SdrNumber.Should().Be("SDR-0101-2026-0001");
        sdr.Amount.Amount.Should().Be(150_000m);
        sdr.Commission.Amount.Should().Be(100m);
        sdr.VatAmount.Amount.Should().Be(15m);
        sdr.TotalPayable.Amount.Should().Be(150_115m);
        sdr.Status.Should().Be(SecurityDepositReceiptStatus.Issued);
        sdr.TenderOrRfpNumber.Should().Be("RFP-PWD-DHAKA-2026-01");
        sdr.BeneficiaryAuthority.Should().Be("Executive Engineer, PWD Dhaka Division-1");
    }

    [Fact]
    public void Sdr_MakerCheckerSeparation_Violation_ThrowsInvariantViolation()
    {
        var act = () => new SecurityDepositReceipt(
            Guid.NewGuid(),
            "SDR-0101-2026-0002",
            "0101",
            Money.Bdt(50_000m),
            "TENDER-001",
            "Roads & Highways Department",
            "CIF-001",
            null,
            BaseIssueDate,
            BaseExpiryDate,
            "OFFICER-01",
            "OFFICER-01"); // Violation: Maker == Checker

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*cannot authorize their own SDR issuance*");
    }

    [Fact]
    public void Sdr_WalkInDueDiligence_Threshold_EnforcesCddRequirements()
    {
        // >= 50,000 BDT for walk-in customer requires valid WalkInCustomerDetails
        var act = () => new SecurityDepositReceipt(
            Guid.NewGuid(),
            "SDR-0101-2026-0003",
            "0101",
            Money.Bdt(60_000m),
            "TENDER-002",
            "LGED Sylhet",
            null, // No CIF
            null, // No WalkIn details
            BaseIssueDate,
            BaseExpiryDate,
            "MAKER-01",
            "CHECKER-02");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Walk-in cash transactions of 50,000 BDT or above strictly require verified Customer Due Diligence*");
    }

    [Fact]
    public void Sdr_LienMark_MarksStatusAsLienMarked()
    {
        var sdr = CreateSampleSdr("SDR-0101-2026-0004", 100_000m);

        sdr.LienMark();

        sdr.Status.Should().Be(SecurityDepositReceiptStatus.LienMarked);
    }

    [Fact]
    public void Sdr_ReleaseAndRefund_RefundsPrincipalToPurchaser()
    {
        var sdr = CreateSampleSdr("SDR-0101-2026-0005", 200_000m);
        sdr.LienMark();

        var refundAmount = sdr.ReleaseAndRefund("PWD-RELEASE-LTR-999", new DateOnly(2026, 5, 10), "MGR-01");

        refundAmount.Amount.Should().Be(200_000m);
        sdr.Status.Should().Be(SecurityDepositReceiptStatus.Refunded);
        sdr.ReleaseLetterReference.Should().Be("PWD-RELEASE-LTR-999");
        sdr.SettledDate.Should().Be(new DateOnly(2026, 5, 10));
    }

    [Fact]
    public void Sdr_ForfeitAndPayToBeneficiary_LiquidatesToBeneficiaryAuthority()
    {
        var sdr = CreateSampleSdr("SDR-0101-2026-0006", 200_000m);
        sdr.LienMark();

        var forfeitAmount = sdr.ForfeitAndPayToBeneficiary("PWD-FORFEIT-CLAIM-777", new DateOnly(2026, 6, 1), "MGR-01");

        forfeitAmount.Amount.Should().Be(200_000m);
        sdr.Status.Should().Be(SecurityDepositReceiptStatus.ClaimPaid);
        sdr.ForfeitureClaimReference.Should().Be("PWD-FORFEIT-CLAIM-777");
        sdr.SettledDate.Should().Be(new DateOnly(2026, 6, 1));
    }

    [Fact]
    public void Sdr_StopPayment_AndRevokeStopPayment_RestoresStatusWithAuditNarration()
    {
        var sdr = CreateSampleSdr("SDR-0101-2026-0007", 80_000m);

        // Act 1: Stop Payment
        sdr.StopPayment("Reported lost by contractor", "SUP-01");
        sdr.Status.Should().Be(SecurityDepositReceiptStatus.Stopped);
        sdr.StopReason.Should().Be("Reported lost by contractor");

        // Act 2: Revoke Stop Payment (BRPD 2.1.1-ix, 2.1.2-vi)
        sdr.RevokeStopPayment("Original SDR recovered and verified intact", "MGR-01");
        sdr.Status.Should().Be(SecurityDepositReceiptStatus.Issued);
        sdr.StopReason.Should().BeNull();
        sdr.StopRevocationReason.Should().Be("Original SDR recovered and verified intact");
    }

    [Fact]
    public void Sdr_IssueDuplicate_WhenStoppedWithIndemnity_Succeeds()
    {
        var sdr = CreateSampleSdr("SDR-0101-2026-0008", 120_000m);
        sdr.StopPayment("Instrument stolen", "SUP-01");

        // Act: Issue Duplicate backed by registered indemnity bond (BRPD 2.1.4-ii)
        sdr.IssueDuplicate("SDR-0101-2026-0008-DUP", "INDEMNITY-BOND-REG-12345", "MGR-01");

        sdr.Status.Should().Be(SecurityDepositReceiptStatus.DuplicateIssued);
        sdr.DuplicateSdrNumber.Should().Be("SDR-0101-2026-0008-DUP");
        sdr.IndemnityBondReference.Should().Be("INDEMNITY-BOND-REG-12345");
    }

    [Fact]
    public void Sdr_IssueDuplicate_WhenNotStopped_ThrowsInvariantViolation()
    {
        var sdr = CreateSampleSdr("SDR-0101-2026-0009", 100_000m);

        var act = () => sdr.IssueDuplicate("SDR-0101-2026-0009-DUP", "INDEMNITY-123", "MGR-01");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*can only be issued against a stopped SDR*");
    }

    [Fact]
    public void RemittanceInstrument_RevokeStopPayment_RevertsStatusToIssued()
    {
        // Testing PO/DD stop payment revocation (BRPD 2.1.1-ix, 2.1.2-vi)
        var instrument = new RemittanceInstrument(
            Guid.NewGuid(),
            "PO-0101-2026-00001",
            RemittanceInstrumentType.PayOrder,
            "0101",
            Money.Bdt(50_000m),
            "Bangladesh Power Development Board",
            "CIF-10001",
            null,
            "TELLER-01",
            "CHECKER-02",
            BaseIssueDate);

        instrument.StopPayment("Purchaser misplaced instrument", "SUP-01");
        instrument.Status.Should().Be(RemittanceInstrumentStatus.Stopped);

        // Revoke Stop Payment
        instrument.RevokeStopPayment("Found by purchaser in office file", "SUP-01");
        instrument.Status.Should().Be(RemittanceInstrumentStatus.Issued);
        instrument.StopReason.Should().BeNull();
        instrument.StopRevocationReason.Should().Be("Found by purchaser in office file");
    }

    private static SecurityDepositReceipt CreateSampleSdr(string sdrNumber, decimal amount)
    {
        return new SecurityDepositReceipt(
            Guid.NewGuid(),
            sdrNumber,
            "0101",
            Money.Bdt(amount),
            "TENDER-PWD-2026",
            "Executive Engineer, PWD",
            "CIF-CORP-999",
            null,
            BaseIssueDate,
            BaseExpiryDate,
            "MAKER-01",
            "CHECKER-02");
    }
}
