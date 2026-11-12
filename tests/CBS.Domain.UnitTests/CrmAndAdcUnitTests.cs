using System;
using System.Linq;
using CBS.Domain.Adc;
using CBS.Domain.Common;
using CBS.Domain.Customers;
using CBS.Domain.Customers.ValueObjects;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Unit tests locking Corporate Group UBO hierarchy,
// Transaction Profile (TP) variance alerts, and ADC central settlement reconciliation
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 7 & 8 and BFIU guidelines.

public class CrmAndAdcUnitTests
{
    // =========================================================================
    // 1. Corporate Group & UBO Hierarchy Tests
    // =========================================================================

    [Fact]
    public void CorporateGroup_Should_Reject_When_Proposed_Exposure_Exceeds_Statutory_Limit()
    {
        // ARCHITECTURAL INTENT: SingleBorrowerGroupExposureInvariant ensures conglomerate
        // sister concerns cannot breach regulatory group exposure limit (BRPD 8.1.1-c, 8.1.18-d).
        var group = new CorporateGroup("GRP-001", "Apex Conglomerate", 500_000_000m, "USR-OFFICER", DateTime.UtcNow);

        // Current Funded: 300M, Current Non-Funded: 150M. Proposed: 60M (Total = 510M > 500M limit)
        var act = () => group.ValidateProposedExposure(300_000_000m, 150_000_000m, 60_000_000m);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*GroupExposureLimitExceededInvariant*")
            .WithMessage("*510,000,000.00 BDT breaches statutory limit of 500,000,000.00 BDT*");
    }

    [Fact]
    public void CorporateGroup_Should_Allow_Proposed_Exposure_Within_Statutory_Limit()
    {
        var group = new CorporateGroup("GRP-002", "Beximco Holdings", 1_000_000_000m, "USR-OFFICER", DateTime.UtcNow);

        // Current: 400M Funded + 200M Non-Funded + 100M Proposed = 700M <= 1000M
        var act = () => group.ValidateProposedExposure(400_000_000m, 200_000_000m, 100_000_000m);

        act.Should().NotThrow();
    }

    [Fact]
    public void BeneficialOwner_Should_Enforce_25Percent_Threshold_For_Majority_Shareholder()
    {
        // ARCHITECTURAL INTENT: UboShareholdingThresholdInvariant mandates >= 25% shareholding
        // for Natural Person Majority Shareholder per BFIU Circular 26 and BRPD 8.1.12.
        var act = () => new BeneficialOwnerRecord(
            Guid.NewGuid(),
            "Rahim Chowdhury",
            "NID-1985123456789",
            BeneficialOwnerType.NaturalPersonMajorityShareholder,
            24.99m, // < 25%
            isPepOrIp: false,
            "Verified",
            DateTime.UtcNow);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*UboShareholdingThresholdInvariant*")
            .WithMessage("*must hold at least 25% shareholding*");
    }

    [Fact]
    public void BeneficialOwner_Should_Register_Successfully_When_25Percent_Or_Greater()
    {
        var group = new CorporateGroup("GRP-003", "Summit Group", 800_000_000m, "USR-OFFICER", DateTime.UtcNow);

        var ubo = new BeneficialOwnerRecord(
            Guid.NewGuid(),
            "Muhammed Aziz Khan",
            "NID-1960987654321",
            BeneficialOwnerType.NaturalPersonMajorityShareholder,
            35.50m,
            isPepOrIp: true,
            "Verified",
            DateTime.UtcNow);

        group.RegisterBeneficialOwner(ubo);

        group.BeneficialOwners.Should().HaveCount(1);
        group.BeneficialOwners[0].FullName.Should().Be("Muhammed Aziz Khan");
        group.BeneficialOwners[0].ShareholdingPercentage.Should().Be(35.50m);
        group.BeneficialOwners[0].IsPepOrIp.Should().BeTrue();
    }

    [Fact]
    public void CorporateGroup_Should_Add_Member_Detail_And_Sync_Member_Cifs()
    {
        var group = new CorporateGroup("GRP-004", "Square Group", 900_000_000m, "USR-OFFICER", DateTime.UtcNow);

        var member = new GroupMemberDetail(
            "CIF-SQR-PHARMA",
            "Square Pharmaceuticals PLC",
            CustomerGroupRole.Subsidiary,
            65.00m,
            "REG-RJSC-12345",
            DateTime.UtcNow);

        group.AddMemberDetail(member);

        group.MemberCifNumbers.Should().Contain("CIF-SQR-PHARMA");
        group.MemberDetails.Should().HaveCount(1);
        group.MemberDetails[0].Role.Should().Be(CustomerGroupRole.Subsidiary);
        group.MemberDetails[0].ShareholdingPercentage.Should().Be(65.00m);
    }

    // =========================================================================
    // 2. Customer Transaction Profile (TP) Variance Tests
    // =========================================================================

    [Fact]
    public void CustomerTransactionProfile_Should_Reject_Non_Positive_Turnover()
    {
        var act = () => new CustomerTransactionProfile(
            "CIF-IND-1001",
            Money.Bdt(0),
            Money.Bdt(100_000), 5,
            Money.Bdt(50_000), 3,
            Money.Bdt(200_000), 10,
            Money.Bdt(50_000), 10,
            DateTime.UtcNow);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*TransactionProfileLimitInvariant*")
            .WithMessage("*turnover must be strictly greater than zero*");
    }

    [Fact]
    public void CustomerTransactionProfile_Should_Detect_Variance_Breach_When_Turnover_Exceeds_150Percent()
    {
        // ARCHITECTURAL INTENT: BRPD 8.1.9, 8.1.18-f & BFIU: Cumulative monthly volume > 150% triggers TP breach
        var initialDate = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        var tp = new CustomerTransactionProfile(
            "CIF-IND-1002",
            Money.Bdt(1_000_000m), // Declared monthly turnover: 1,000,000 BDT (Threshold 150% = 1,500,000 BDT)
            Money.Bdt(500_000m), 10,
            Money.Bdt(300_000m), 5,
            Money.Bdt(600_000m), 15,
            Money.Bdt(200_000m), 20,
            initialDate);

        // Transaction 1: 800,000 BDT Transfer (Total = 800k <= 1.5M, no breach)
        tp.RecordTransaction(TpTransactionCategory.ClearingTransferDeposit, Money.Bdt(800_000m), initialDate);
        tp.IsTpBreached.Should().BeFalse();

        // Transaction 2: 800,000 BDT Cash Deposit (Total = 1.6M > 1.5M threshold -> BREACH)
        tp.RecordTransaction(TpTransactionCategory.CashDeposit, Money.Bdt(800_000m), initialDate);
        tp.IsTpBreached.Should().BeTrue();
        tp.MaxVariancePercentage.Should().BeGreaterThan(0);
        tp.LastBreachReason.Should().Contain("exceeded declared");
    }

    [Fact]
    public void CustomerTransactionProfile_Should_Clear_Breach_After_Edd_Review()
    {
        var initialDate = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        var tp = new CustomerTransactionProfile(
            "CIF-IND-1003",
            Money.Bdt(500_000m),
            Money.Bdt(200_000m), 5,
            Money.Bdt(100_000m), 3,
            Money.Bdt(300_000m), 10,
            Money.Bdt(100_000m), 10,
            initialDate);

        // Cause breach: 400,000 cash deposit (declared 200k * 150% = 300k)
        tp.RecordTransaction(TpTransactionCategory.CashDeposit, Money.Bdt(400_000m), initialDate);
        tp.IsTpBreached.Should().BeTrue();

        // Clear breach after EDD
        tp.ClearBreachAfterEdd("USR-COMPLIANCE", "Property sale deed verified and source of funds confirmed.");

        tp.IsTpBreached.Should().BeFalse();
        tp.LastBreachReason.Should().Contain("[Cleared after EDD]");
        tp.LastReviewedByUserId.Should().Be("USR-COMPLIANCE");
    }

    [Fact]
    public void CustomerTransactionProfile_Should_Reset_Accumulators_On_New_Month()
    {
        var novDate = new DateTime(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);
        var tp = new CustomerTransactionProfile(
            "CIF-IND-1004",
            Money.Bdt(500_000m),
            Money.Bdt(200_000m), 5,
            Money.Bdt(100_000m), 3,
            Money.Bdt(300_000m), 10,
            Money.Bdt(100_000m), 10,
            novDate);

        tp.RecordTransaction(TpTransactionCategory.CashDeposit, Money.Bdt(150_000m), novDate);
        tp.CurrentMonthCashDepositAmount.Amount.Should().Be(150_000m);
        tp.CurrentMonthYear.Should().Be("2026-11");

        // Transaction in December
        var decDate = new DateTime(2026, 12, 2, 11, 0, 0, DateTimeKind.Utc);
        tp.RecordTransaction(TpTransactionCategory.CashDeposit, Money.Bdt(50_000m), decDate);

        tp.CurrentMonthYear.Should().Be("2026-12");
        tp.CurrentMonthCashDepositAmount.Amount.Should().Be(50_000m); // Reset and only has Dec transaction
        tp.CurrentMonthCashDepositCount.Should().Be(1);
    }

    // =========================================================================
    // 3. ADC Central Settlement Reconciliation & Rectification Tests
    // =========================================================================

    [Fact]
    public void AdcReconciliationBatch_Should_Classify_Matches_And_Mismatches_Correctly()
    {
        // ARCHITECTURAL INTENT: BRPD 7.2.1-viii, ix: Classifies Matched, ExcessInSwitch, and AmountMismatch
        var batch = new AdcReconciliationBatch(
            "REC-NPSB-20261112",
            AdcSettlementNetwork.NPSB,
            new DateOnly(2026, 11, 12),
            "GL-2011001-NPSB-SETTLEMENT",
            "GL-1011004-ADC-SUSPENSE",
            "USR-RECON-OFFICER",
            DateTime.UtcNow);

        // Record 1: Exact Match (Switch 5,000 BDT, CBS 5,000 BDT)
        batch.IngestSwitchRecord("123456", "RRN123456789", "ATM-001", "0101100001", Money.Bdt(5000), Money.Bdt(5000), DateTime.UtcNow);

        // Record 2: Excess in Switch (Switch debited 10,000 BDT, CBS has null)
        batch.IngestSwitchRecord("123457", "RRN123456780", "ATM-001", "0101100002", Money.Bdt(10000), null, DateTime.UtcNow);

        // Record 3: Amount Mismatch (Switch 3,000 BDT, CBS 2,000 BDT -> Diff 1,000 BDT)
        batch.IngestSwitchRecord("123458", "RRN123456781", "ATM-002", "0101100003", Money.Bdt(3000), Money.Bdt(2000), DateTime.UtcNow);

        batch.TotalRecords.Should().Be(3);
        batch.TotalMatched.Should().Be(1);
        batch.TotalMismatched.Should().Be(2);
        batch.TotalDiscrepancyAmount.Amount.Should().Be(11_000m); // 10,000 + 1,000
        batch.Status.Should().Be(ReconciliationBatchStatus.DiscrepancyPendingRectification);
    }

    [Fact]
    public void AdcReconciliationBatch_Should_Generate_Balanced_Rectification_Voucher()
    {
        // ARCHITECTURAL INTENT: RectificationVoucherDoubleEntryInvariant generates balanced double-entry
        // GL voucher routing through suspense account (BRPD 7.2.1-xx).
        var batch = new AdcReconciliationBatch(
            "REC-VISA-20261112",
            AdcSettlementNetwork.VISA,
            new DateOnly(2026, 11, 12),
            "GL-2011002-VISA-SETTLEMENT",
            "GL-1011004-ADC-SUSPENSE",
            "USR-RECON-OFFICER",
            DateTime.UtcNow);

        batch.IngestSwitchRecord("654321", "RRN987654321012", "ATM-001", "0101100005", Money.Bdt(5000), null, DateTime.UtcNow);
        var entry = batch.Entries.First();

        var voucher = batch.GenerateRectificationVoucher(
            entry.EntryId,
            RectificationAction.CreditCustomerDebitSettlementSuspense,
            "0101100005",
            "USR-CHECKER",
            "Customer refund for ATM cashout dispense failure");

        voucher.Should().NotBeNull();
        voucher.DebitAccountOrGl.Should().Be("GL-1011004-ADC-SUSPENSE");
        voucher.CreditAccountOrGl.Should().Be("0101100005");
        voucher.VoucherAmount.Amount.Should().Be(5000m);
        voucher.AuthorizedByUserId.Should().Be("USR-CHECKER");

        entry.IsRectified.Should().BeTrue();
        entry.RectificationVoucherNumber.Should().Be(voucher.VoucherNumber);
        batch.Status.Should().Be(ReconciliationBatchStatus.ClosedWithRectifications);
    }

    [Fact]
    public void AdcReconciliationBatch_Should_Prevent_Closing_With_Unrectified_Mismatches()
    {
        var batch = new AdcReconciliationBatch(
            "REC-MFS-20261112",
            AdcSettlementNetwork.MFS_bKash,
            new DateOnly(2026, 11, 12),
            "GL-2011003-BKASH-SETTLEMENT",
            "GL-1011004-ADC-SUSPENSE",
            "USR-RECON-OFFICER",
            DateTime.UtcNow);

        batch.IngestSwitchRecord("999999", "RRN111222333444", "POS-001", "0101100009", Money.Bdt(2000), null, DateTime.UtcNow);

        var act = () => batch.CloseBatch("USR-RECON-OFFICER");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*UnrectifiedMismatchesInvariant*")
            .WithMessage("*unrectified discrepancies remain*");
    }
}
