using CBS.Domain.Corporate;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.UnitTests;

public class BulkCorporateBatchTests
{
    private static AccountNumber ValidAcc(int seq) => AccountNumber.Generate("0101", "001", seq);

    [Fact]
    public void UploadBatch_AddsItemsAndCalculatesTotalAmount()
    {
        // Arrange
        var corpAcc = ValidAcc(301);
        var batch = new BulkCorporateBatch(
            "BATCH-001",
            BulkBatchType.SalaryDisbursement,
            corpAcc,
            "October 2026 Executive Payroll",
            DateOnly.FromDateTime(DateTime.UtcNow),
            Currency.BDT,
            "CORP_MAKER",
            DateTime.UtcNow);

        // Act
        batch.AddItem(ValidAcc(302).Value, "Rahim Ahmed", Money.Bdt(50_000m), "EMP-001");
        batch.AddItem(ValidAcc(303).Value, "Karim Ullah", Money.Bdt(30_000m), "EMP-002");
        batch.AddItem(ValidAcc(304).Value, "Fatima Begum", Money.Bdt(20_000m), "EMP-003");

        // Assert
        Assert.Equal(3, batch.TotalRecords);
        Assert.Equal(100_000m, batch.TotalAmount.Amount);
        Assert.Equal(BulkBatchStatus.Uploaded, batch.Status);
    }

    [Fact]
    public void MarkItemValidation_AllValid_SetsValidatedStatus()
    {
        // Arrange
        var corpAcc = ValidAcc(305);
        var batch = new BulkCorporateBatch(
            "BATCH-002",
            BulkBatchType.SalaryDisbursement,
            corpAcc,
            "November 2026 Payroll",
            DateOnly.FromDateTime(DateTime.UtcNow),
            Currency.BDT,
            "CORP_MAKER",
            DateTime.UtcNow);

        batch.AddItem(ValidAcc(306).Value, "Employee 1", Money.Bdt(10_000m), "REF-1");
        batch.AddItem(ValidAcc(307).Value, "Employee 2", Money.Bdt(15_000m), "REF-2");

        // Act
        batch.MarkItemValidation(1, true);
        batch.MarkItemValidation(2, true);

        // Assert
        Assert.Equal(BulkBatchStatus.Validated, batch.Status);
    }

    [Fact]
    public void MarkItemValidation_WithInvalidItem_SetsPartiallyValidatedStatus()
    {
        // Arrange
        var corpAcc = ValidAcc(308);
        var batch = new BulkCorporateBatch(
            "BATCH-003",
            BulkBatchType.DividendDistribution,
            corpAcc,
            "FY25 Final Dividend",
            DateOnly.FromDateTime(DateTime.UtcNow),
            Currency.BDT,
            "CORP_MAKER",
            DateTime.UtcNow);

        batch.AddItem(ValidAcc(309).Value, "Shareholder 1", Money.Bdt(5_000m), "DIV-1");
        batch.AddItem(ValidAcc(310).Value, "Shareholder 2", Money.Bdt(8_000m), "DIV-2");

        // Act
        batch.MarkItemValidation(1, true);
        batch.MarkItemValidation(2, false, "Account is in Frozen status.");

        // Assert
        Assert.Equal(BulkBatchStatus.PartiallyValidated, batch.Status);
    }

    [Fact]
    public void AuthorizeBatch_MakerCheckerSameUser_ThrowsInvariantViolation()
    {
        // Arrange
        var corpAcc = ValidAcc(311);
        var batch = new BulkCorporateBatch(
            "BATCH-004",
            BulkBatchType.SalaryDisbursement,
            corpAcc,
            "December 2026 Payroll",
            DateOnly.FromDateTime(DateTime.UtcNow),
            Currency.BDT,
            "MAKER_USER",
            DateTime.UtcNow);

        batch.AddItem(ValidAcc(312).Value, "Emp A", Money.Bdt(10_000m), "E-1");
        batch.MarkItemValidation(1, true);

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() =>
            batch.Authorize("MAKER_USER", DateTime.UtcNow));

        Assert.Equal("MakerCheckerSeparationInvariant", ex.InvariantName);
        Assert.Contains("BRPD Circular 12/2024", ex.RegulatoryReference);
    }

    [Fact]
    public void RecordItemPosting_UpdatesCountsAndCompletedStatus()
    {
        // Arrange
        var corpAcc = ValidAcc(313);
        var batch = new BulkCorporateBatch(
            "BATCH-005",
            BulkBatchType.SalaryDisbursement,
            corpAcc,
            "January 2027 Payroll",
            DateOnly.FromDateTime(DateTime.UtcNow),
            Currency.BDT,
            "MAKER_01",
            DateTime.UtcNow);

        batch.AddItem(ValidAcc(314).Value, "Emp 1", Money.Bdt(40_000m), "REF-01");
        batch.AddItem(ValidAcc(315).Value, "Emp 2", Money.Bdt(20_000m), "REF-02");
        batch.MarkItemValidation(1, true);
        batch.MarkItemValidation(2, true);
        batch.Authorize("CHECKER_02", DateTime.UtcNow);

        // Act
        batch.RecordItemPosting(1, true, "JRN-001", null);
        batch.RecordItemPosting(2, false, null, "Target Account Dormant");
        batch.CompleteExecution(DateTime.UtcNow);

        // Assert
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailedCount);
        Assert.Equal(40_000m, batch.SuccessfulAmount.Amount);
        Assert.Equal(20_000m, batch.FailedAmount.Amount);
        Assert.Equal(BulkBatchStatus.CompletedWithErrors, batch.Status);
    }
}
