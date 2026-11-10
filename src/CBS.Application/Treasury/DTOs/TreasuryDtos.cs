using CBS.Domain.Enums;

namespace CBS.Application.Treasury.DTOs;

public sealed record CaptureTreasuryDealRequest(
    string DealReference,
    TreasuryDealType Type,
    DealDirection Direction,
    string CounterpartyBankBic,
    string CounterpartyName,
    decimal PrimaryAmount,
    string PrimaryCurrency,
    decimal SecondaryAmount,
    string SecondaryCurrency,
    decimal ExchangeRate,
    decimal InterestRate,
    DateTime DealDate,
    DateTime ValueDate,
    string FrontOfficeDealerUserId,
    string BranchCode = "001",
    string? SecurityIsin = null);

public sealed record VerifyMidOfficeDealRequest(
    string DealReference,
    string MidOfficeOfficerUserId);

public sealed record SettleBackOfficeDealRequest(
    string DealReference,
    string BackOfficeOfficerUserId);

public sealed record TreasuryDealResponse(
    string DealReference,
    string Type,
    string Direction,
    string CounterpartyBankBic,
    string CounterpartyName,
    decimal PrimaryAmount,
    string PrimaryCurrency,
    decimal SecondaryAmount,
    string SecondaryCurrency,
    decimal ExchangeRate,
    decimal InterestRate,
    DateTime DealDate,
    DateTime ValueDate,
    string Status,
    string FrontOfficeDealerUserId,
    string? MidOfficeRiskOfficerUserId,
    string? BackOfficeSettlementUserId,
    DateTime? SettledAtUtc,
    string BranchCode = "001",
    string? SettlementVoucherNumber = null,
    string? SecurityIsin = null);

public sealed record EvaluateCrrSlrRequest(
    decimal NdtlAmount,
    string Currency,
    BankingType BankingType,
    decimal MaintainedCrrCash,
    decimal MaintainedSlrSecurities);

public sealed record CrrSlrResponse(
    decimal NdtlAmount,
    string Currency,
    string BankingType,
    decimal CrrRequiredDailyRate,
    decimal ActualCrrPercentage,
    bool HasCrrShortfall,
    decimal CrrShortfallOrSurplusAmount,
    decimal SlrRequiredRate,
    decimal ActualSlrPercentage,
    bool HasSlrShortfall,
    decimal SlrShortfallOrSurplusAmount);

public sealed record LiquidityBucketInput(
    LiquidityBucket Bucket,
    decimal Inflow,
    decimal Outflow);

public sealed record EvaluateLiquidityGapRequest(
    string Currency,
    IReadOnlyList<LiquidityBucketInput> Buckets);

public sealed record LiquidityBucketOutput(
    string Bucket,
    decimal Inflows,
    decimal Outflows,
    decimal NetGap,
    decimal CumulativeGap);

public sealed record LiquidityGapResponse(
    string Currency,
    IReadOnlyList<LiquidityBucketOutput> Buckets,
    bool HasShortTermDeficitAlert,
    string Summary);

// --- Government Securities & Mark-to-Market (MTM) DTOs ---

public sealed record RegisterTreasurySecurityRequest(
    string SecurityId,
    string SecurityName,
    TreasurySecurityType SecurityType,
    SecurityClassification Classification,
    decimal FaceValue,
    decimal BookValue,
    string Currency,
    decimal CouponRate,
    decimal PurchaseYield,
    DateTime IssueDate,
    DateTime MaturityDate,
    string BranchCode = "001");

public sealed record TreasurySecurityResponseDto(
    string SecurityId,
    string SecurityName,
    string SecurityType,
    string Classification,
    decimal FaceValue,
    decimal BookValue,
    decimal MarketValue,
    string Currency,
    decimal CouponRate,
    decimal PurchaseYield,
    decimal CurrentMarketYield,
    decimal CleanPrice,
    DateTime IssueDate,
    DateTime MaturityDate,
    DateTime LastMtmValuationDate,
    decimal UnrealizedMtmGainLoss,
    string BranchCode);

public sealed record SecurityMarketQuoteDto(
    string SecurityId,
    decimal MarketYield,
    decimal CleanPrice);

public sealed record MtmRevaluationBatchRequest(
    string BranchCode = "001",
    string ValuerUserId = "TREASURY_RISK_01",
    IReadOnlyList<SecurityMarketQuoteDto>? Quotes = null);

public sealed record MtmSecurityResultDto(
    string SecurityId,
    string SecurityName,
    decimal OldMarketValue,
    decimal NewMarketValue,
    decimal MtmDifferential,
    string? VoucherNumber);

public sealed record MtmRevaluationBatchResponseDto(
    int TotalHftSecuritiesEvaluated,
    int TotalRevaluationVouchersPosted,
    decimal TotalNetMtmDifferential,
    IReadOnlyList<MtmSecurityResultDto> Results);

// --- Market Risk & Limits DTOs ---

public sealed record EvaluateParametricVaRRequest(
    decimal PortfolioValue,
    decimal AnnualVolatilityPercentage,
    int HoldingDays = 1,
    decimal ConfidenceLevel = 0.99m);

public sealed record ParametricVaRResponseDto(
    decimal PortfolioValue,
    decimal ConfidenceLevel,
    int HoldingDays,
    decimal AnnualVolatilityPercentage,
    decimal DailyVolatilityPercentage,
    decimal ValueAtRiskAmount,
    decimal ValueAtRiskPercentage);

public sealed record FxPositionInputDto(
    string Currency,
    decimal LongExposure,
    decimal ShortExposure);

public sealed record FxNetOpenPositionRequest(
    decimal ApprovedNopLimit,
    IReadOnlyList<FxPositionInputDto> Positions);

public sealed record FxCurrencyPositionDto(
    string Currency,
    decimal LongExposure,
    decimal ShortExposure,
    decimal NetExposure);

public sealed record FxNetOpenPositionResponseDto(
    decimal TotalNetLongExposure,
    decimal TotalNetShortExposure,
    decimal OverallNetOpenPosition,
    decimal ApprovedNopLimit,
    bool HasNopLimitBreach,
    decimal UtilizationPercentage,
    IReadOnlyList<FxCurrencyPositionDto> CurrencyBreakdown);
