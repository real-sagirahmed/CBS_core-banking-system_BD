using System;
using System.Collections.Generic;
using CBS.Domain.Enums;

namespace CBS.Application.BackValue.DTOs;

// --- 1. Back-Value Transaction DTOs ---

public sealed record SubmitBackValueTransactionRequest(
    BackValueTargetType TargetType,
    string TargetIdentifier,
    string BranchCode,
    DateOnly BookingDate,
    DateOnly ValueDate,
    decimal Amount,
    DebitCredit TransactionNature,
    string Reason,
    string RegulatoryReference,
    string MakerUserId,
    decimal? AnnualInterestRatePercent = null,
    bool IsDepositAccount = true);

public sealed record ApproveBackValueTransactionRequest(
    string CheckerUserId,
    string? Remarks = null);

public sealed record RejectBackValueTransactionRequest(
    string CheckerUserId,
    string RejectionReason);

public sealed record BackValueTransactionResponseDto(
    string RequestId,
    string TargetType,
    string TargetIdentifier,
    string BranchCode,
    DateOnly BookingDate,
    DateOnly ValueDate,
    int DaysVariance,
    decimal Amount,
    string TransactionNature,
    string Reason,
    string RegulatoryReference,
    string MakerUserId,
    string Status,
    string? CheckerUserId,
    string? CheckerRemarks,
    DateTime? ActionedAtUtc,
    string? ExecutedVoucherNumber,
    decimal? RetroactiveInterestCalculated,
    string? RetroactiveInterestVoucherNumber,
    DateTime CreatedAtUtc);

// --- 2. Retroactive Interest Differential Simulation DTOs ---

public sealed record SimulateRetroactiveInterestRequest(
    decimal PrincipalAmount,
    decimal AnnualInterestRatePercent,
    DateOnly ValueDate,
    DateOnly BookingDate,
    DebitCredit TransactionNature,
    bool IsDepositAccount = true);

public sealed record RetroactiveInterestCalculationResultDto(
    decimal DifferentialAmount,
    string Direction,
    int DaysPast,
    decimal PrincipalAmount,
    decimal AnnualInterestRatePercent,
    string AccountingNarration);

// --- 3. Linked Cascade Reversal DTOs ---

public sealed record LinkedAuxiliaryEntryDto(
    string AuxiliaryVoucherNumber,
    ReversalType AuxiliaryType,
    decimal Amount,
    string Narration);

public sealed record ReverseTransactionCascadeRequest(
    string OriginalVoucherId,
    string OriginalVoucherNumber,
    string ReversalReason,
    string ReversedByUserId,
    string AuthorizerUserId,
    decimal TotalReversedAmount,
    List<LinkedAuxiliaryEntryDto>? LinkedAuxiliaryEntries = null);

public sealed record CascadeReversalResponseDto(
    string ReversalRecordId,
    string OriginalVoucherId,
    string OriginalVoucherNumber,
    string ParentReversalVoucherNumber,
    decimal TotalReversedAmount,
    int LinkedEntriesReversedCount,
    string ReversedByUserId,
    string AuthorizerUserId,
    DateTime ReversedAtUtc,
    bool IsCascadeComplete);

// --- 4. EOD Back-Value Supervisory Audit Journal DTOs ---

public sealed record BackValueAuditJournalEntryDto(
    string EntryId,
    string RequestId,
    string BranchCode,
    DateOnly BookingDate,
    DateOnly ValueDate,
    int DaysVariance,
    string TargetAccountOrGl,
    decimal PrincipalAmount,
    string Currency,
    decimal RetroactiveInterestAmount,
    string Direction,
    string MakerUserId,
    string CheckerUserId,
    DateTime ApprovalTimeUtc,
    string VoucherNumber,
    string AuditNarration);

public sealed record EodBackValueJournalReportDto(
    DateOnly OperatingDate,
    string BranchCode,
    int TotalBackValueEntriesCount,
    decimal TotalPrincipalAmount,
    decimal TotalRetroactiveInterestAccrued,
    List<BackValueAuditJournalEntryDto> JournalEntries);
