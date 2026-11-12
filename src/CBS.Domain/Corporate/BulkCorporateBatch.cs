using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Corporate;

// ARCHITECTURAL INTENT: High-Throughput Bulk Corporate Salary, Dividend, and Refund Batch Processing Engine
// strictly conforming to Bangladesh Bank BRPD Circular No. 12/2024 Module 1.6.16.
// Validates account integrity before upload execution and maintains itemized success/failure reporting.
public class BulkCorporateBatch : AggregateRoot<string>
{
    public BulkBatchType BatchType { get; private set; }
    public AccountNumber CorporateDebitAccountNumber { get; private set; }
    public string Narration { get; private set; }
    public DateOnly ValueDate { get; private set; }
    public Money TotalAmount { get; private set; }
    public int TotalRecords { get; private set; }
    public BulkBatchStatus Status { get; private set; }
    public string CreatedByUserId { get; private set; }
    public string? AuthorizedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? AuthorizedAtUtc { get; private set; }
    public DateTime? ExecutedAtUtc { get; private set; }

    private readonly List<BulkCorporateBatchItem> _items = [];
    public IReadOnlyCollection<BulkCorporateBatchItem> Items => _items.AsReadOnly();

    #pragma warning disable CS8618
    protected BulkCorporateBatch() : base(string.Empty) { }
    #pragma warning restore CS8618

    public BulkCorporateBatch(
        string batchId,
        BulkBatchType batchType,
        AccountNumber corporateDebitAccountNumber,
        string narration,
        DateOnly valueDate,
        Currency currency,
        string createdByUserId,
        DateTime createdAtUtc) : base(batchId)
    {
        if (string.IsNullOrWhiteSpace(batchId)) throw new ArgumentException("Batch ID is required.", nameof(batchId));
        ArgumentNullException.ThrowIfNull(corporateDebitAccountNumber, nameof(corporateDebitAccountNumber));
        if (string.IsNullOrWhiteSpace(narration)) throw new ArgumentException("Narration is required.", nameof(narration));
        if (string.IsNullOrWhiteSpace(createdByUserId)) throw new ArgumentException("Maker User ID is required.", nameof(createdByUserId));

        BatchType = batchType;
        CorporateDebitAccountNumber = corporateDebitAccountNumber;
        Narration = narration.Trim();
        ValueDate = valueDate;
        TotalAmount = Money.Zero(currency);
        TotalRecords = 0;
        Status = BulkBatchStatus.Uploaded;
        CreatedByUserId = createdByUserId.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public void AddItem(string beneficiaryAccountNumber, string beneficiaryName, Money amount, string referenceNo)
    {
        if (Status != BulkBatchStatus.Uploaded)
        {
            throw new InvalidOperationException($"Cannot add items to batch '{Id}' in status '{Status}'. Items can only be added to 'Uploaded' batches.");
        }

        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        if (amount.Amount <= 0m) throw new ArgumentException("Item amount must be positive.", nameof(amount));
        if (string.IsNullOrWhiteSpace(beneficiaryAccountNumber)) throw new ArgumentException("Beneficiary account number is required.", nameof(beneficiaryAccountNumber));
        if (string.IsNullOrWhiteSpace(beneficiaryName)) throw new ArgumentException("Beneficiary name is required.", nameof(beneficiaryName));

        int nextIndex = _items.Count + 1;
        var item = new BulkCorporateBatchItem(
            Id,
            nextIndex,
            beneficiaryAccountNumber.Trim(),
            beneficiaryName.Trim(),
            amount,
            string.IsNullOrWhiteSpace(referenceNo) ? $"REF-{nextIndex:D5}" : referenceNo.Trim());

        _items.Add(item);
        TotalRecords = _items.Count;
        TotalAmount += amount;
    }

    public void MarkItemValidation(int itemIndex, bool isValid, string? failureReason = null)
    {
        var item = _items.FirstOrDefault(i => i.ItemIndex == itemIndex)
            ?? throw new InvalidOperationException($"Batch item at index '{itemIndex}' not found.");

        item.SetValidationResult(isValid, failureReason);

        bool allValidated = _items.All(i => i.Status is BulkItemStatus.Valid or BulkItemStatus.Invalid);
        if (allValidated)
        {
            bool anyInvalid = _items.Any(i => i.Status == BulkItemStatus.Invalid);
            Status = anyInvalid ? BulkBatchStatus.PartiallyValidated : BulkBatchStatus.Validated;
        }
    }

    public void Authorize(string checkerUserId, DateTime authorizedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId)) throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));

        if (Status is not (BulkBatchStatus.Validated or BulkBatchStatus.PartiallyValidated))
        {
            throw new InvalidOperationException($"Cannot authorize batch '{Id}' in status '{Status}'. Batch must be validated first.");
        }

        // HARD LOCK: Dual-Control Maker-Checker segregation (BRPD 1.3.18, 10.3.5-x)
        if (CreatedByUserId.Equals(checkerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Maker '{CreatedByUserId}' cannot authorize their own bulk batch. Strict dual-control duty segregation required.");
        }

        AuthorizedByUserId = checkerUserId.Trim();
        AuthorizedAtUtc = authorizedAtUtc;
    }

    public void RecordItemPosting(int itemIndex, bool isSuccess, string? transactionJournalId, string? failureReason)
    {
        var item = _items.FirstOrDefault(i => i.ItemIndex == itemIndex)
            ?? throw new InvalidOperationException($"Batch item at index '{itemIndex}' not found.");

        item.RecordExecutionResult(isSuccess, transactionJournalId, failureReason);

        bool allProcessed = _items.All(i => i.Status is BulkItemStatus.Posted or BulkItemStatus.Failed or BulkItemStatus.Invalid);
        if (allProcessed)
        {
            bool anyFailed = _items.Any(i => i.Status is BulkItemStatus.Failed or BulkItemStatus.Invalid);
            Status = anyFailed ? BulkBatchStatus.CompletedWithErrors : BulkBatchStatus.Executed;
        }
    }

    public void CompleteExecution(DateTime executedAtUtc)
    {
        ExecutedAtUtc = executedAtUtc;
        bool anyFailed = _items.Any(i => i.Status is BulkItemStatus.Failed or BulkItemStatus.Invalid);
        Status = anyFailed ? BulkBatchStatus.CompletedWithErrors : BulkBatchStatus.Executed;
    }

    public int SuccessCount => _items.Count(i => i.Status == BulkItemStatus.Posted);
    public int FailedCount => _items.Count(i => i.Status is BulkItemStatus.Failed or BulkItemStatus.Invalid);
    public Money SuccessfulAmount => Money.Bdt(_items.Where(i => i.Status == BulkItemStatus.Posted).Sum(i => i.Amount.Amount));
    public Money FailedAmount => Money.Bdt(_items.Where(i => i.Status is BulkItemStatus.Failed or BulkItemStatus.Invalid).Sum(i => i.Amount.Amount));
}

public class BulkCorporateBatchItem
{
    public string BatchId { get; private set; }
    public int ItemIndex { get; private set; }
    public string BeneficiaryAccountNumber { get; private set; }
    public string BeneficiaryName { get; private set; }
    public Money Amount { get; private set; }
    public string ReferenceNo { get; private set; }
    public BulkItemStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public string? TransactionJournalId { get; private set; }

    #pragma warning disable CS8618
    protected BulkCorporateBatchItem() { }
    #pragma warning restore CS8618

    public BulkCorporateBatchItem(
        string batchId,
        int itemIndex,
        string beneficiaryAccountNumber,
        string beneficiaryName,
        Money amount,
        string referenceNo)
    {
        BatchId = batchId;
        ItemIndex = itemIndex;
        BeneficiaryAccountNumber = beneficiaryAccountNumber;
        BeneficiaryName = beneficiaryName;
        Amount = amount;
        ReferenceNo = referenceNo;
        Status = BulkItemStatus.Pending;
    }

    public void SetValidationResult(bool isValid, string? failureReason)
    {
        Status = isValid ? BulkItemStatus.Valid : BulkItemStatus.Invalid;
        FailureReason = isValid ? null : failureReason;
    }

    public void RecordExecutionResult(bool isSuccess, string? transactionJournalId, string? failureReason)
    {
        Status = isSuccess ? BulkItemStatus.Posted : BulkItemStatus.Failed;
        TransactionJournalId = isSuccess ? transactionJournalId : null;
        FailureReason = isSuccess ? null : failureReason;
    }
}
