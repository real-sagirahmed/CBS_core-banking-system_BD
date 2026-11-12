// ARCHITECTURAL INTENT: DTOs and commands for Treasury Management, FX Blotter, NOP,
// Mark-to-Market (MTM) Revaluation, Nostro Reconciliation, and Statutory Reserves (CRR/SLR)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 6, DOS 05/2008, and MPD 03/2020.

using CBS.Domain.Enums;

namespace CBS.Application.Treasury.DTOs;

public sealed record FxBlotterCurrencyPositionDto(
    string Currency,
    decimal OpeningPosition,
    decimal TotalBoughtFcy,
    decimal TotalSoldFcy,
    decimal NetPosition,
    decimal WeightedAverageBuyRate,
    decimal WeightedAverageSellRate,
    decimal RevaluationMidRate,
    decimal BdtEquivalent
);

public sealed record FxBlotterSummaryDto(
    DateOnly BlotterDate,
    IReadOnlyList<FxBlotterCurrencyPositionDto> CurrencyPositions,
    decimal TotalNetOpenPositionUsd,
    decimal TotalNetOpenPositionBdt,
    decimal SanctionedNopLimitUsd,
    decimal NopUtilizationPercentage,
    bool IsNopCompliant
);

public sealed record CheckTreasuryLimitCommand(
    TreasuryLimitType LimitType,
    string Identifier,
    string DealReference,
    decimal Amount,
    string Currency
);

public sealed record ConfigureTreasuryLimitProfileCommand(
    TreasuryLimitType LimitType,
    string Identifier,
    string Currency,
    decimal SanctionedLimit,
    string ApprovedByUserId,
    DateOnly EffectiveDate,
    DateOnly? ExpiryDate = null
);

public sealed record TreasuryLimitProfileDto(
    Guid Id,
    TreasuryLimitType LimitType,
    string Identifier,
    string Currency,
    decimal SanctionedLimit,
    decimal UtilizedAmount,
    decimal AvailableAmount,
    bool IsActive,
    string ApprovedByUserId,
    DateOnly EffectiveDate,
    DateOnly? ExpiryDate
);

public sealed record TreasuryLimitCheckResultDto(
    TreasuryLimitType LimitType,
    string Identifier,
    decimal SanctionedLimit,
    decimal UtilizedAmount,
    decimal AvailableAmount,
    decimal RequestedAmount,
    bool IsAllowed,
    string Message
);

public sealed record ExecuteMtmRevaluationCommand(
    DateOnly? RevaluationDate,
    string ExecutedByUserId
);

public sealed record MtmRevaluationResultDto(
    Guid BatchId,
    string BatchReference,
    DateOnly RevaluationDate,
    int TotalForwardDealsEvaluated,
    int TotalSecuritiesEvaluated,
    decimal TotalUnrealizedGainsBdt,
    decimal TotalUnrealizedLossesBdt,
    decimal NetRevaluationReserveBdt,
    string Status
);

public sealed record EvaluateStatutoryReservesCommand(
    DateOnly? AssessmentDate,
    BankingType BankingType,
    decimal TdtlBaseBdt,
    decimal MaintainedCrrCashBdt,
    decimal MaintainedSlrAssetsBdt,
    string EvaluatedByUserId
);

public sealed record StatutoryReserveAssessmentDto(
    Guid AssessmentId,
    DateOnly AssessmentDate,
    BankingType BankingType,
    decimal TdtlBaseBdt,
    decimal RequiredCrrDailyBdt,
    decimal RequiredCrrBiWeeklyBdt,
    decimal MaintainedCrrCashBdt,
    decimal DailyCrrShortfallBdt,
    bool IsDailyCrrCompliant,
    decimal RequiredSlrBdt,
    decimal MaintainedSlrAssetsBdt,
    decimal SlrShortfallBdt,
    bool IsSlrCompliant
);

public sealed record NostroStatementEntryDto(
    string StatementReference,
    DateOnly ValueDate,
    string EntryType, // "Debit" or "Credit"
    decimal Amount
);

public sealed record IngestNostroStatementCommand(
    string NostroAccountCode,
    string Currency,
    IReadOnlyList<NostroStatementEntryDto> Entries
);

public sealed record IngestLocalNostroEntryCommand(
    string NostroAccountCode,
    string Currency,
    DateOnly ValueDate,
    string EntryType,
    string LocalBookReference,
    decimal LocalBookAmount
);

public sealed record NostroReconciliationBatchResultDto(
    string NostroAccountCode,
    int TotalItemsProcessed,
    int AutoMatchedCount,
    int UnmatchedCount
);

public sealed record NostroUnreconciledItemDto(
    Guid ItemId,
    string NostroAccountCode,
    string Currency,
    DateOnly ValueDate,
    string EntryType,
    string LocalBookReference,
    decimal LocalBookAmount,
    int DaysOutstanding,
    decimal RequiredProvisionRate,
    decimal RequiredProvisionAmount,
    string Status
);

public sealed record NostroUnreconciledAgingReportDto(
    DateOnly ReportDate,
    int TotalUnreconciledItems,
    decimal TotalUnreconciledDebitAmount,
    decimal TotalRequiredProvisionAmount,
    IReadOnlyList<NostroUnreconciledItemDto> Items
);
