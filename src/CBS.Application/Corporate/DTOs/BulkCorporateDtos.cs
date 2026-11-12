namespace CBS.Application.Corporate.DTOs;

public record UploadBulkItemDto(
    string BeneficiaryAccountNumber,
    string BeneficiaryName,
    decimal Amount,
    string ReferenceNo);

public record UploadBulkBatchRequest(
    string BatchType,
    string CorporateDebitAccountNumber,
    string Narration,
    DateOnly ValueDate,
    string MakerUserId,
    List<UploadBulkItemDto> Items);

public record BulkBatchItemDto(
    int ItemIndex,
    string BeneficiaryAccountNumber,
    string BeneficiaryName,
    decimal Amount,
    string ReferenceNo,
    string Status,
    string? FailureReason,
    string? TransactionJournalId);

public record BulkBatchSummaryDto(
    string BatchId,
    string BatchType,
    string CorporateDebitAccountNumber,
    string Narration,
    DateOnly ValueDate,
    string Status,
    decimal TotalAmount,
    int TotalRecords,
    int SuccessCount,
    int FailedCount,
    decimal SuccessfulAmount,
    decimal FailedAmount,
    string CreatedByUserId,
    string? AuthorizedByUserId,
    DateTime CreatedAtUtc,
    DateTime? ExecutedAtUtc,
    List<BulkBatchItemDto> Items);

public record AuthorizeBulkBatchRequest(string CheckerUserId);

public record ExecuteBulkBatchRequest(string CheckerUserId);

public record ExecuteBulkBatchResponseDto(
    string BatchId,
    string Status,
    int TotalRecords,
    int SuccessCount,
    int FailedCount,
    decimal SuccessfulAmount,
    decimal FailedAmount,
    decimal CorporateDebitedAmount,
    DateTime ExecutedAtUtc,
    List<BulkBatchItemDto> ItemResults);
