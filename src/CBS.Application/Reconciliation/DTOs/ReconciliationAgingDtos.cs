namespace CBS.Application.Reconciliation.DTOs;

public record RecordUnreconciledItemRequest(
    string ChannelType,
    string TraceOrReferenceNumber,
    string OriginatingBranchCode,
    string RespondingBranchOrCounterparty,
    decimal TransactionAmount,
    DateOnly TransactionDate,
    DateOnly AssessmentDate);

public record ResolveUnreconciledItemRequest(
    string TraceOrReferenceNumber,
    string ResolutionReference,
    DateOnly ResolutionDate);

public record AssessAgingRequest(
    DateOnly AssessmentDate,
    string? ChannelType = null);

public record ReconciliationAgingItemResponse(
    Guid Id,
    string ChannelType,
    string TraceOrReferenceNumber,
    string OriginatingBranchCode,
    string RespondingBranchOrCounterparty,
    decimal TransactionAmount,
    DateOnly TransactionDate,
    DateOnly LastAssessmentDate,
    int AgeInDays,
    string Bucket,
    decimal RequiredProvisionPercentage,
    decimal RequiredProvisionAmount,
    bool IsResolved,
    DateOnly? ResolvedDate,
    string? ResolutionReference);

public record ReconciliationBucketSummary(
    string Bucket,
    int ItemCount,
    decimal TotalAmount,
    decimal RequiredProvisionPercentage,
    decimal TotalProvisionAmount);

public record ReconciliationAgingSummaryResponse(
    DateOnly AssessmentDate,
    int TotalUnresolvedItems,
    decimal TotalUnresolvedAmount,
    decimal TotalRequiredProvision,
    IReadOnlyList<ReconciliationBucketSummary> BucketSummaries);
