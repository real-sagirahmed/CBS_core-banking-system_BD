using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Automated unit tests locking Bank Guarantee invariants
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 5.4.
public class BankGuaranteeTests
{
    private static readonly DateOnly BaseIssueDate = new(2026, 1, 1);
    private static readonly DateOnly BaseExpiryDate = new(2026, 7, 1); // 181 days ~ 3 quarters

    [Fact]
    public void BankGuarantee_Issuance_CalculatesQuarterlyCommissionAnd15PercentVat()
    {
        // Arrange & Act
        // Guarantee: 1,000,000 BDT, Margin: 20% -> 200,000 BDT
        // Tenor: 181 + 30 = 211 days -> ceil(211 / 90) = 3 quarters
        // Commission @ 0.50%/quarter: 1,000,000 * 0.005 * 3 = 15,000 BDT
        // VAT @ 15%: 15,000 * 0.15 = 2,250 BDT
        // Total Fee: 17,250 BDT
        var bg = new BankGuarantee(
            Guid.NewGuid(),
            "BG-0101-2026-0001",
            GuaranteeType.PerformanceBond,
            new CifNumber("CIF-CORP-101"),
            "Bangladesh Railway Directorate",
            "CRB, Chittagong",
            "0101",
            Money.Bdt(1_000_000m),
            20.0m,
            "0101000000018",
            0.50m,
            BaseIssueDate,
            BaseExpiryDate,
            30,
            GuaranteeExpiryType.FixedDate,
            "MAKER-01",
            "CHECKER-02");

        // Assert
        bg.BgNumber.Should().Be("BG-0101-2026-0001");
        bg.Type.Should().Be(GuaranteeType.PerformanceBond);
        bg.GuaranteeAmount.Amount.Should().Be(1_000_000m);
        bg.CashMarginAmount.Amount.Should().Be(200_000m);
        bg.CommissionAmount.Amount.Should().Be(15_000m);
        bg.VatAmount.Amount.Should().Be(2_250m);
        bg.TotalFeePayable.Amount.Should().Be(17_250m);
        bg.Status.Should().Be(BankGuaranteeStatus.Issued);
    }

    [Fact]
    public void BankGuarantee_MakerCheckerSeparation_Violation_ThrowsInvariantViolation()
    {
        var act = () => new BankGuarantee(
            Guid.NewGuid(),
            "BG-0101-2026-0002",
            GuaranteeType.BidBond_Tender,
            new CifNumber("CIF-CORP-102"),
            "Petrobangla",
            "Kawran Bazar, Dhaka",
            "0101",
            Money.Bdt(500_000m),
            10.0m,
            null,
            0.50m,
            BaseIssueDate,
            BaseExpiryDate,
            15,
            GuaranteeExpiryType.FixedDate,
            "OFFICER-01",
            "OFFICER-01"); // Violation: Maker == Checker

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*cannot authorize their own Bank Guarantee issuance*");
    }

    [Fact]
    public void BankGuarantee_InvalidAmount_ThrowsInvariantViolation()
    {
        var act = () => new BankGuarantee(
            Guid.NewGuid(),
            "BG-0101-2026-0003",
            GuaranteeType.FinancialGuarantee,
            new CifNumber("CIF-CORP-103"),
            "Customs Authority",
            "Dhaka Custom House",
            "0101",
            Money.Bdt(0m),
            100.0m,
            null,
            0.50m,
            BaseIssueDate,
            BaseExpiryDate,
            30,
            GuaranteeExpiryType.FixedDate,
            "MAKER-01",
            "CHECKER-02");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Guarantee amount must be strictly positive*");
    }

    [Fact]
    public void BankGuarantee_InvalidExpiryDate_ThrowsInvariantViolation()
    {
        var act = () => new BankGuarantee(
            Guid.NewGuid(),
            "BG-0101-2026-0004",
            GuaranteeType.AdvancePaymentGuarantee,
            new CifNumber("CIF-CORP-104"),
            "Power Development Board",
            "Motijheel, Dhaka",
            "0101",
            Money.Bdt(200_000m),
            15.0m,
            null,
            0.50m,
            BaseIssueDate,
            BaseIssueDate, // Expiry == IssueDate
            30,
            GuaranteeExpiryType.FixedDate,
            "MAKER-01",
            "CHECKER-02");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*expiry date*must be strictly after issue date*");
    }

    [Fact]
    public void BankGuarantee_AmendGuarantee_IncreasesAmountAndExtendsExpiry_WithAdditionalFee()
    {
        var bg = CreateSampleGuarantee("BG-0101-2026-0005", 1_000_000m, 20.0m);
        decimal initialFee = bg.TotalFeePayable.Amount;

        // Act: Amend BG - Increase by 500,000 BDT and extend expiry by 3 months
        var extendedDate = BaseExpiryDate.AddMonths(3);
        bg.AmendGuarantee(Money.Bdt(500_000m), extendedDate, "AMEND-BOARD-APPROVAL-44", "CHECKER-02");

        // Assert
        bg.GuaranteeAmount.Amount.Should().Be(1_500_000m);
        bg.CashMarginAmount.Amount.Should().Be(300_000m); // 20% of 1.5M
        bg.ExpiryDate.Should().Be(extendedDate);
        bg.TotalFeePayable.Amount.Should().BeGreaterThan(initialFee);
        bg.Status.Should().Be(BankGuaranteeStatus.Amended);
        bg.AmendmentReference.Should().Be("AMEND-BOARD-APPROVAL-44");
    }

    [Fact]
    public void BankGuarantee_InvokeClaim_BeforeMaxClaimPeriod_SetsStatusToClaimInvoked()
    {
        var bg = CreateSampleGuarantee("BG-0101-2026-0006", 1_000_000m, 25.0m);

        // Claim within claim period (e.g. 10 days after expiry)
        var validClaimDate = BaseExpiryDate.AddDays(10);
        bg.InvokeClaim("CLAIM-BR-OFFICIAL-LETTER-99", validClaimDate, "SUP-01");

        bg.Status.Should().Be(BankGuaranteeStatus.ClaimInvoked);
        bg.ClaimLetterReference.Should().Be("CLAIM-BR-OFFICIAL-LETTER-99");
        bg.ClaimDate.Should().Be(validClaimDate);
    }

    [Fact]
    public void BankGuarantee_InvokeClaim_AfterClaimPeriodExpired_ThrowsInvariantViolation()
    {
        var bg = CreateSampleGuarantee("BG-0101-2026-0007", 1_000_000m, 25.0m);

        // Claim after claim period (e.g. 45 days after expiry, when claim period is 30 days)
        var lateClaimDate = BaseExpiryDate.AddDays(45);
        var act = () => bg.InvokeClaim("LATE-CLAIM-LTR", lateClaimDate, "SUP-01");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*exceeds maximum claim period boundary*Guarantee has lapsed*");
    }

    [Fact]
    public void BankGuarantee_LiquidateClaim_PaysBeneficiaryAuthority()
    {
        var bg = CreateSampleGuarantee("BG-0101-2026-0008", 2_000_000m, 50.0m);
        bg.InvokeClaim("CLAIM-PWD-9988", BaseExpiryDate.AddDays(5), "SUP-01");

        var payout = bg.LiquidateClaim("MGR-01");

        payout.Amount.Should().Be(2_000_000m);
        bg.Status.Should().Be(BankGuaranteeStatus.LiquidatedClaimPaid);
    }

    [Fact]
    public void BankGuarantee_CancelAndRelease_ExtinguishesContingentLiability()
    {
        var bg = CreateSampleGuarantee("BG-0101-2026-0009", 500_000m, 10.0m);

        bg.CancelAndRelease("PWD-BG-RELEASE-MEMO-123", true, new DateOnly(2026, 6, 15), "MGR-01");

        bg.Status.Should().Be(BankGuaranteeStatus.CancelledReleased);
        bg.ReleaseLetterReference.Should().Be("PWD-BG-RELEASE-MEMO-123");
        bg.OriginalReturned.Should().BeTrue();
        bg.CancelledDate.Should().Be(new DateOnly(2026, 6, 15));
    }

    private static BankGuarantee CreateSampleGuarantee(string bgNumber, decimal amount, decimal marginPct)
    {
        return new BankGuarantee(
            Guid.NewGuid(),
            bgNumber,
            GuaranteeType.PerformanceBond,
            new CifNumber("CIF-SAMPLE-101"),
            "Roads & Highways Department",
            "Tejgaon, Dhaka",
            "0101",
            Money.Bdt(amount),
            marginPct,
            null,
            0.50m,
            BaseIssueDate,
            BaseExpiryDate,
            30,
            GuaranteeExpiryType.FixedDate,
            "MAKER-01",
            "CHECKER-02");
    }
}
