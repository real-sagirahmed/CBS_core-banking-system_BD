using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Remittance;
using CBS.Domain.Remittance.ValueObjects;
using CBS.Domain.ValueObjects;
using Xunit;

namespace CBS.Domain.UnitTests;

public class ForeignRemittanceTests
{
    [Fact]
    public void CreateInward_WageEarnerRemittance_CalculatesStatutoryTwoPointFivePercentIncentive()
    {
        // Arrange
        var accNo = AccountNumber.Generate("0101", "201", 10001);
        var foreignAmt = new Money(1000.00m, Currency.USD);
        decimal exchangeRate = 120.50m; // 1 USD = 120.50 BDT

        // Act
        var txn = ForeignRemittanceTransaction.CreateInward(
            Guid.NewGuid(),
            "INW-TEST-001",
            ForeignRemittanceType.WageEarnerInward,
            "Mohammad Rahim",
            "ARE",
            "ALANSARI",
            "Fatema Begum",
            accNo,
            new CifNumber("CIF100200300"),
            foreignAmt,
            exchangeRate,
            RemittanceChargeParty.OUR,
            "MAKER01",
            DateTime.UtcNow,
            null,
            SanctionStatus.Clean,
            "Family Support");

        // Assert
        // 1000 * 120.50 = 120,500.00 BDT
        Assert.Equal(120500.00m, txn.ConvertedBdtAmount.Amount);
        Assert.Equal(0.025m, txn.IncentiveRate);
        // 120,500 * 0.025 = 3,012.50 BDT
        Assert.Equal(3012.50m, txn.IncentiveAmount.Amount);
        Assert.Equal(123512.50m, txn.TotalDisbursedBdtAmount.Amount);
        Assert.Equal(GovernmentIncentiveStatus.Calculated, txn.IncentiveStatus);
        Assert.Equal(ForeignRemittanceStatus.PendingCheckerApproval, txn.Status);
    }

    [Fact]
    public void CreateInward_CommercialExportProceeds_ReceivesZeroIncentive()
    {
        // Arrange
        var accNo = AccountNumber.Generate("0101", "201", 10001);
        var foreignAmt = new Money(50000.00m, Currency.USD);
        decimal exchangeRate = 120.00m;

        // Act
        var txn = ForeignRemittanceTransaction.CreateInward(
            Guid.NewGuid(),
            "INW-EXP-001",
            ForeignRemittanceType.ExportProceedsInward,
            "Global Apparel Buyer Inc",
            "USA",
            "CITIUS33",
            "Bengal Textile Mills Ltd",
            accNo,
            new CifNumber("CIF999888777"),
            foreignAmt,
            exchangeRate,
            RemittanceChargeParty.SHA,
            "MAKER01",
            DateTime.UtcNow,
            null,
            SanctionStatus.Clean,
            "Export Proceeds Realization");

        // Assert
        Assert.Equal(6000000.00m, txn.ConvertedBdtAmount.Amount);
        Assert.Equal(0.00m, txn.IncentiveRate);
        Assert.Equal(0.00m, txn.IncentiveAmount.Amount);
        Assert.Equal(6000000.00m, txn.TotalDisbursedBdtAmount.Amount);
        Assert.Equal(GovernmentIncentiveStatus.NotApplicable, txn.IncentiveStatus);
    }

    [Fact]
    public void CreateInward_SanctionHit_LocksTransactionInHeldForSanctionReview()
    {
        // Arrange
        var accNo = AccountNumber.Generate("0101", "201", 10001);
        var foreignAmt = new Money(2000.00m, Currency.USD);

        // Act
        var txn = ForeignRemittanceTransaction.CreateInward(
            Guid.NewGuid(),
            "INW-SANC-001",
            ForeignRemittanceType.WageEarnerInward,
            "Sanctioned Entity XYZ",
            "IRN",
            null,
            "Beneficiary Person",
            accNo,
            null,
            foreignAmt,
            120.00m,
            RemittanceChargeParty.BEN,
            "MAKER01",
            DateTime.UtcNow,
            null,
            SanctionStatus.ConfirmedSanctionHit,
            "Family Support");

        // Assert
        Assert.Equal(ForeignRemittanceStatus.HeldForSanctionReview, txn.Status);

        // Attempting to authorize must throw InvariantViolationException
        var ex = Assert.Throws<InvariantViolationException>(() =>
            txn.AuthorizeInwardRemittance("CHECKER01", DateTime.UtcNow));
        Assert.Contains("SanctionScreeningHitInvariant", ex.InvariantName);
    }

    [Fact]
    public void AuthorizeInward_SameMakerAndChecker_ThrowsMakerCheckerSeparationInvariant()
    {
        // Arrange
        var accNo = AccountNumber.Generate("0101", "201", 10001);
        var txn = ForeignRemittanceTransaction.CreateInward(
            Guid.NewGuid(),
            "INW-DUAL-001",
            ForeignRemittanceType.WageEarnerInward,
            "Sender Name",
            "SAU",
            null,
            "Beneficiary Name",
            accNo,
            null,
            new Money(500.00m, Currency.USD),
            120.00m,
            RemittanceChargeParty.OUR,
            "MAKER_JOHN",
            DateTime.UtcNow);

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() =>
            txn.AuthorizeInwardRemittance("MAKER_JOHN", DateTime.UtcNow));
        Assert.Contains("MakerCheckerSeparationInvariant", ex.InvariantName);
    }

    [Fact]
    public void CreateOutward_ExceedingAnnualEntitlementQuota_ThrowsInvariantViolationException()
    {
        // Arrange
        var senderAcc = AccountNumber.Generate("0101", "201", 10001);
        var foreignAmt = new Money(15000.00m, Currency.USD); // @ 120 = 1,800,000 BDT
        decimal exchangeRate = 120.00m;
        decimal quotaLimitAnnual = 2000000.00m; // 2,000,000 BDT yearly quota
        decimal quotaAlreadyUtilized = 500000.00m; // 500,000 utilized. Remaining = 1,500,000 BDT.
        // Remittance of 1,800,000 > 1,500,000 -> Exceeds quota!

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ForeignRemittanceTransaction.CreateOutward(
                Guid.NewGuid(),
                "OUT-STUDENT-001",
                ForeignRemittanceType.StudentFileOutward,
                "Student Father",
                "BGD",
                null,
                "University of Oxford",
                senderAcc,
                new CifNumber("CIF001122334"),
                foreignAmt,
                exchangeRate,
                RemittanceChargeParty.OUR,
                "MAKER01",
                DateTime.UtcNow,
                null,
                SanctionStatus.Clean,
                "Tuition Fee Fall 2026",
                "STU-OXF-2026-01",
                quotaLimitAnnual,
                quotaAlreadyUtilized));

        Assert.Contains("OutwardEntitlementQuotaExceededInvariant", ex.InvariantName);
    }

    [Fact]
    public void Cancel_AuthorizedOutwardRemittance_ThrowsIrreversibleOutwardRemittanceLock()
    {
        // Arrange
        var senderAcc = AccountNumber.Generate("0101", "201", 10001);
        var txn = ForeignRemittanceTransaction.CreateOutward(
            Guid.NewGuid(),
            "OUT-LOCK-001",
            ForeignRemittanceType.StudentFileOutward,
            "Sender",
            "BGD",
            null,
            "Beneficiary",
            senderAcc,
            null,
            new Money(1000.00m, Currency.USD),
            120.00m,
            RemittanceChargeParty.OUR,
            "MAKER01",
            DateTime.UtcNow,
            null,
            SanctionStatus.Clean,
            "Living allowance",
            "STU-001",
            1000000.00m,
            0m);

        txn.AuthorizeOutwardRemittance("CHECKER01", DateTime.UtcNow);
        Assert.Equal(ForeignRemittanceStatus.SwiftTransmitted, txn.Status);

        // Act & Assert (BRPD 2.2.1-vii-f, g)
        var ex = Assert.Throws<InvariantViolationException>(() =>
            txn.Cancel("Customer changed mind", "OFFICER01"));

        Assert.Contains("IrreversibleOutwardRemittanceLock", ex.InvariantName);
    }

    [Fact]
    public void GovernmentIncentiveClaimBatch_BatchesDisbursedIncentives_AndSubmitsToBangladeshBank()
    {
        // Arrange
        var accNo = AccountNumber.Generate("0101", "201", 10001);
        var txn = ForeignRemittanceTransaction.CreateInward(
            Guid.NewGuid(),
            "INW-BATCH-001",
            ForeignRemittanceType.WageEarnerInward,
            "Expat Worker",
            "MYS",
            null,
            "Beneficiary",
            accNo,
            null,
            new Money(1000.00m, Currency.USD),
            120.00m,
            RemittanceChargeParty.OUR,
            "MAKER01",
            DateTime.UtcNow);

        txn.AuthorizeInwardRemittance("CHECKER01", DateTime.UtcNow);
        Assert.Equal(GovernmentIncentiveStatus.DisbursedToBeneficiary, txn.IncentiveStatus);

        // Batch creation
        var batch = new GovernmentIncentiveClaimBatch(
            Guid.NewGuid(),
            "BB-INC-202611-001",
            "2026-11",
            1,
            txn.IncentiveAmount,
            "FINANCE_OFFICER",
            DateTime.UtcNow);

        txn.MarkIncentiveBatched(batch.Id);
        Assert.Equal(GovernmentIncentiveStatus.ClaimBatched, txn.IncentiveStatus);
        Assert.Equal(batch.Id, txn.CentralBankClaimBatchId);

        // Submit to Bangladesh Bank
        batch.SubmitToBangladeshBank("BB/FEPD/2026/0892", DateTime.UtcNow);
        Assert.Equal(IncentiveClaimBatchStatus.SubmittedToBangladeshBank, batch.Status);

        // Reimburse
        batch.MarkReimbursed("BB/SETTLE/2026/1102", DateTime.UtcNow);
        Assert.Equal(IncentiveClaimBatchStatus.Reimbursed, batch.Status);

        txn.MarkIncentiveReimbursed();
        Assert.Equal(GovernmentIncentiveStatus.ReimbursedByCentralBank, txn.IncentiveStatus);
    }
}
