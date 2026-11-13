// ARCHITECTURAL INTENT: Data Transfer Objects for Payment Order (PO) and Demand Draft (DD) Operations
// adhering to Bangladesh Bank BRPD Circular No. 12 (1.5.6, 3.11, 3.12).

using System;
using System.Collections.Generic;
using CBS.Domain.Enums;

namespace CBS.Application.PaymentInstruments.DTOs;

public record AllocateStockBookRequest(
    string BranchCode,
    PaymentInstrumentType InstrumentType,
    string Prefix,
    long StartLeafNumber,
    int TotalLeaves,
    string CustodianUserId,
    string AllocatedByUserId);

public record PaymentInstrumentStockBookDto(
    string BookId,
    string BranchCode,
    string InstrumentType,
    string Prefix,
    long StartLeafNumber,
    long EndLeafNumber,
    int TotalLeaves,
    int IssuedLeavesCount,
    long NextAvailableLeafNumber,
    string CustodianUserId,
    string AllocatedByUserId,
    bool IsExhausted,
    DateTime AllocatedAtUtc);

public record IssuePayOrderRequest(
    string IssuingBranchCode,
    decimal Amount,
    string PayeeName,
    string RemitterName,
    string? RemitterAccountNumber,
    bool IsWalkInCustomer,
    string? WalkInNid,
    string? WalkInPhotoId,
    string? WalkInMobile,
    string TransactionPurpose,
    string MakerUserId,
    string CheckerUserId);

public record IssueDemandDraftRequest(
    string IssuingBranchCode,
    string DraweeBranchCode,
    decimal Amount,
    string PayeeName,
    string RemitterName,
    string? RemitterAccountNumber,
    bool IsWalkInCustomer,
    string? WalkInNid,
    string? WalkInPhotoId,
    string? WalkInMobile,
    string TransactionPurpose,
    string MakerUserId,
    string CheckerUserId);

public record EncashPaymentInstrumentRequest(
    string PaidByUserId,
    string? PresentedTestKey,
    string SettlementAccountOrGl);

public record CancelPaymentInstrumentRequest(
    string CancelledByUserId,
    string Remarks,
    decimal CancellationFee = 0m);

public record IssueDuplicateInstrumentRequest(
    string OriginalInstrumentId,
    string PoliceGdNumber,
    string IndemnityBondNumber,
    string MakerUserId,
    string CheckerUserId);

public record PaymentInstrumentResponseDto(
    string InstrumentId,
    string InstrumentType,
    string InstrumentNumber,
    string IssuingBranchCode,
    string DraweeBranchCode,
    decimal Amount,
    string Currency,
    string PayeeName,
    string RemitterName,
    string? RemitterAccountNumber,
    bool IsWalkInCustomer,
    string? WalkInNid,
    string? WalkInMobile,
    string TransactionPurpose,
    string Status,
    decimal CommissionAmount,
    decimal VatAmount,
    decimal TotalDebitedFromCustomer,
    DateOnly IssueDate,
    string IssuedByUserId,
    string AuthorizedByUserId,
    string? DdTestKey,
    bool DrawingAdviceTransmitted,
    DateOnly? PaidDate,
    string? PaidByUserId,
    string? PaidVoucherNumber,
    DateOnly? CancelledDate,
    string? CancelledByUserId,
    string? CancellationRemarks,
    decimal? CancellationFeeDeducted,
    string? PoliceGdNumber,
    string? IndemnityBondNumber,
    string? DuplicateOfInstrumentId,
    DateTime CreatedAtUtc);

public record UnpresentedAgingItemDto(
    string InstrumentId,
    string InstrumentType,
    string InstrumentNumber,
    string IssuingBranchCode,
    decimal Amount,
    string PayeeName,
    string RemitterName,
    DateOnly IssueDate,
    int DaysUnpresented,
    string AgingBucket);

public record UnpresentedAgingReportDto(
    DateOnly AsOfDate,
    string? BranchCode,
    int TotalUnpresentedCount,
    decimal TotalUnpresentedAmount,
    int StaleCountOver6Months,
    decimal StaleAmountOver6Months,
    int UnclaimedCountOver3Years,
    decimal UnclaimedAmountOver3Years,
    int BangladeshBankTransferEligibleOver10Years,
    decimal BangladeshBankTransferEligibleAmount,
    IReadOnlyList<UnpresentedAgingItemDto> Items);
