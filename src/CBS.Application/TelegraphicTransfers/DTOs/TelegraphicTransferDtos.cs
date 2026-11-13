// ARCHITECTURAL INTENT: Data Transfer Objects for Telegraphic/Telephonic Transfer (TT) Operations
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 3 (3.13) and Module 1 (1.6.5).

using System;
using System.Collections.Generic;

namespace CBS.Application.TelegraphicTransfers.DTOs;

public record IssueTtRequest(
    string IssuingBranchCode,
    string DraweeBranchCode,
    decimal Amount,
    string PayeeName,
    string? PayeeAccountNumber,
    string? RemitterName,
    string? RemitterAccountNumber,
    string? TransactionPurpose,
    bool SanctionScreeningPassed,
    string MakerUserId,
    string CheckerUserId);

public record TransmitTtAdviceRequest(
    string TransmittedByUserId);

public record LogTelephonicCallbackRequest(
    string ReceivingOfficerUserId,
    string VerifiedWithOfficerName,
    string ContactNumber,
    string Notes);

public record RespondAndPayTtRequest(
    string RespondingOfficerUserId,
    string PresentedTestKey,
    string DestinationAccountOrSundryGl);

public record CancelTtRequest(
    string CancelledByUserId,
    string Remarks);

public record TelegraphicTransferResponseDto(
    string TtId,
    string TtNumber,
    DateOnly IssueDate,
    string IssuingBranchCode,
    string DraweeBranchCode,
    decimal Amount,
    string Currency,
    string PayeeName,
    string? PayeeAccountNumber,
    string? RemitterName,
    string? RemitterAccountNumber,
    string? TransactionPurpose,
    decimal CommissionAmount,
    decimal MessageChargeAmount,
    decimal VatAmount,
    decimal TotalDebitedFromRemitter,
    string TtTestKey,
    string Status,
    bool SanctionScreeningPassed,
    string MakerUserId,
    string CheckerUserId,
    DateTime? AdviceTransmittedAtUtc,
    string? AdviceTransmittedByUserId,
    DateTime? TelephonicallyVerifiedAtUtc,
    string? TelephonicVerifiedByOfficer,
    string? TelephonicContactNumber,
    string? TelephonicNotes,
    DateOnly? RespondedDate,
    string? RespondingAdviceNumber,
    string? RespondingOfficerUserId,
    string? RespondingVoucherNumber,
    string? DestinationAccountOrSundryGl,
    DateOnly? CancelledDate,
    string? CancelledByUserId,
    string? CancellationRemarks,
    DateTime CreatedAtUtc);

public record UnrespondedTtAgingItemDto(
    string TtId,
    string TtNumber,
    string IssuingBranchCode,
    string DraweeBranchCode,
    decimal Amount,
    string PayeeName,
    DateOnly IssueDate,
    int DaysUnresponded,
    string Status,
    string AgingBucket);

public record UnrespondedTtAgingReportDto(
    DateOnly AsOfDate,
    string? BranchCode,
    int TotalUnrespondedCount,
    decimal TotalUnrespondedAmount,
    int Over3DaysCount,
    decimal Over3DaysAmount,
    int Over7DaysCount,
    decimal Over7DaysAmount,
    int Over30DaysCount,
    decimal Over30DaysAmount,
    IReadOnlyList<UnrespondedTtAgingItemDto> Items);
