using CBS.Domain.Enums;

namespace CBS.Application.Cheques.DTOs;

public sealed record RequisitionChequeBookRequest(
    string BookId,
    string AccountNumber,
    string Prefix,
    long StartLeafNumber,
    int TotalLeaves,
    string BranchCode);

public sealed record ChequeBookResponseDto(
    string BookId,
    string AccountNumber,
    string Prefix,
    long StartLeafNumber,
    long EndLeafNumber,
    int TotalLeaves,
    string Status,
    string BranchCode,
    DateOnly RequisitionDate,
    DateTime? DeliveredAtUtc);

public sealed record ChequeLeafStatusDto(
    string LeafId,
    string BookId,
    string AccountNumber,
    long LeafNumber,
    string Status,
    string? StopReason,
    string? StopNotes,
    DateTime? StoppedAtUtc,
    string? StoppedByBranch,
    DateTime? PaidAtUtc,
    string? PaidVoucherNumber);

public sealed record ChequeStopPaymentRequest(
    string AccountNumber,
    long LeafNumber,
    StopPaymentReason Reason,
    string Notes,
    string BranchCode,
    string OfficerId);

public sealed record ChequeRevokeStopPaymentRequest(
    string AccountNumber,
    long LeafNumber,
    string OfficerId,
    string Reason);

public sealed record UndeliveredChequeReportItemDto(
    string BookId,
    string AccountNumber,
    string BranchCode,
    DateOnly RequisitionDate,
    int AgeInDays);
