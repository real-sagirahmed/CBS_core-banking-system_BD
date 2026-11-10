using CBS.Domain.Enums;

namespace CBS.Application.Reconciliation.DTOs;

public sealed record SwitchTxItemDto(
    string Stan,
    string Rrn,
    string AccountNumber,
    decimal Amount);

public sealed record NetworkSettlementItemDto(
    string Stan,
    string Rrn,
    string AccountNumber,
    decimal Amount);

public sealed record RunReconciliationRequest(
    DateOnly ReconciliationDate,
    AdcChannelType Channel,
    IReadOnlyList<SwitchTxItemDto> SwitchTransactions,
    IReadOnlyList<NetworkSettlementItemDto> NetworkTransactions);

public sealed record UnmatchedReconItemDto(
    string ItemId,
    string Stan,
    string Rrn,
    string AccountNumber,
    decimal Amount,
    string DiscrepancyType,
    string AgingBucket,
    int DaysUnreconciled,
    bool IsRectified);

public sealed record SettlementReconciliationResponse(
    string BatchId,
    DateOnly ReconciliationDate,
    string Channel,
    int TotalCore,
    int TotalSwitch,
    int TotalNetwork,
    int MatchedCount,
    int MismatchedCount,
    string Status,
    IReadOnlyList<UnmatchedReconItemDto> UnmatchedItems);

public sealed record FileDisputeClaimRequest(
    string ClaimId,
    string OriginalTransactionId,
    string OriginalStan,
    string OriginalRrn,
    string AccountNumber,
    decimal DisputedAmount,
    AdcChannelType Channel,
    string Reason);

public sealed record ResolveDisputeRequest(
    string CheckerUserId,
    string ProvidedStan,
    string ProvidedRrn,
    string Notes);

public sealed record DisputeClaimResponse(
    string ClaimId,
    string OriginalTransactionId,
    string OriginalStan,
    string OriginalRrn,
    string AccountNumber,
    decimal DisputedAmount,
    string Channel,
    string DisputeReason,
    string Status,
    string? RectificationVoucherNumber,
    string? ResolvedByOfficerId,
    string Notes,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc);
