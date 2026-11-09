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
    string FrontOfficeDealerUserId);

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
    DateTime? SettledAtUtc);

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
