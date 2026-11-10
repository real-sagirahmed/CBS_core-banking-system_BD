using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Application.Treasury.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Treasury;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Treasury.Commands;

public sealed record CaptureTreasuryDealCommand(CaptureTreasuryDealRequest Request);
public sealed record VerifyMidOfficeDealCommand(VerifyMidOfficeDealRequest Request);
public sealed record SettleBackOfficeDealCommand(SettleBackOfficeDealRequest Request);
public sealed record EvaluateCrrSlrCommand(EvaluateCrrSlrRequest Request);
public sealed record EvaluateLiquidityGapCommand(EvaluateLiquidityGapRequest Request);
public sealed record RegisterTreasurySecurityCommand(RegisterTreasurySecurityRequest Request);
public sealed record MtmRevaluationBatchCommand(MtmRevaluationBatchRequest Request);
public sealed record EvaluateParametricVaRCommand(EvaluateParametricVaRRequest Request);
public sealed record EvaluateFxNetOpenPositionCommand(FxNetOpenPositionRequest Request);

public class TreasuryCommandHandler
{
    private readonly ITreasuryRepository _repository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    // Regulatory single-transaction ceiling per front-office dealer without Head of Treasury sign-off (BRPD 6.5.2)
    private const decimal MaxSingleTransactionBdtEquivalent = 1200000000m; // 1,200,000,000 BDT (120 Crore)

    public TreasuryCommandHandler(
        ITreasuryRepository repository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _glService = glService ?? throw new ArgumentNullException(nameof(glService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TreasuryDealResponse> HandleCaptureAsync(CaptureTreasuryDealCommand command, CancellationToken ct = default)
    {
        var req = command.Request;

        // BRPD 6.5.2 Dealer single-ticket limit check
        decimal bdtEquivalent = req.PrimaryCurrency.Equals("BDT", StringComparison.OrdinalIgnoreCase)
            ? req.PrimaryAmount
            : req.SecondaryCurrency.Equals("BDT", StringComparison.OrdinalIgnoreCase)
                ? req.SecondaryAmount
                : req.PrimaryAmount * (req.ExchangeRate > 0 ? req.ExchangeRate : 120m);

        if (bdtEquivalent > MaxSingleTransactionBdtEquivalent)
        {
            throw new InvalidOperationException($"Regulatory Invariant Violation [BRPD 6.5.2]: Front-office dealer single ticket limit of {MaxSingleTransactionBdtEquivalent:N0} BDT equivalent exceeded (Attempted: {bdtEquivalent:N0} BDT). Requires Head of Treasury special override.");
        }

        var pCurr = Currency.FromCode(req.PrimaryCurrency);
        var sCurr = Currency.FromCode(req.SecondaryCurrency);

        var primaryAmount = new Money(req.PrimaryAmount, pCurr);
        var secondaryAmount = new Money(req.SecondaryAmount, sCurr);

        var deal = TreasuryDeal.CaptureByFrontOffice(
            dealReference: req.DealReference,
            type: req.Type,
            direction: req.Direction,
            counterpartyBankBic: req.CounterpartyBankBic,
            counterpartyName: req.CounterpartyName,
            primaryAmount: primaryAmount,
            secondaryAmount: secondaryAmount,
            exchangeRate: req.ExchangeRate,
            interestRate: req.InterestRate,
            dealDate: req.DealDate,
            valueDate: req.ValueDate,
            frontOfficeDealerUserId: req.FrontOfficeDealerUserId,
            branchCode: req.BranchCode,
            securityIsin: req.SecurityIsin);

        await _repository.AddDealAsync(deal, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapDealResponse(deal);
    }

    public async Task<TreasuryDealResponse> HandleVerifyMidOfficeAsync(VerifyMidOfficeDealCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var deal = await _repository.GetDealByReferenceForUpdateAsync(req.DealReference, ct)
            ?? throw new KeyNotFoundException($"Treasury Deal '{req.DealReference}' not found.");

        deal.VerifyByMidOffice(req.MidOfficeOfficerUserId);
        await _repository.UpdateDealAsync(deal, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapDealResponse(deal);
    }

    public async Task<TreasuryDealResponse> HandleSettleBackOfficeAsync(SettleBackOfficeDealCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var deal = await _repository.GetDealByReferenceForUpdateAsync(req.DealReference, ct)
            ?? throw new KeyNotFoundException($"Treasury Deal '{req.DealReference}' not found.");

        // Determine BDT settlement amount and currency
        decimal settlementAmount = deal.PrimaryAmount.Currency.Code.Equals("BDT", StringComparison.OrdinalIgnoreCase)
            ? deal.PrimaryAmount.Amount
            : deal.SecondaryAmount.Currency.Code.Equals("BDT", StringComparison.OrdinalIgnoreCase)
                ? deal.SecondaryAmount.Amount
                : deal.PrimaryAmount.Amount * (deal.ExchangeRate > 0 ? deal.ExchangeRate : 1.0m);

        string fcyCurrency = !deal.PrimaryAmount.Currency.Code.Equals("BDT", StringComparison.OrdinalIgnoreCase)
            ? deal.PrimaryAmount.Currency.Code
            : deal.SecondaryAmount.Currency.Code;

        // Post Double-Entry General Ledger settlement voucher
        var voucher = await _glService.PostTreasuryDealSettlementAsync(
            branchSol: deal.BranchCode,
            dealReference: deal.DealReference,
            dealType: deal.Type,
            direction: deal.Direction,
            settlementAmount: settlementAmount,
            currency: fcyCurrency,
            counterpartyName: deal.CounterpartyName,
            makerUserId: req.BackOfficeOfficerUserId,
            checkerUserId: req.BackOfficeOfficerUserId,
            ct: ct);

        deal.SettleByBackOffice(req.BackOfficeOfficerUserId, voucher.VoucherNumber);
        await _repository.UpdateDealAsync(deal, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapDealResponse(deal);
    }

    public async Task<TreasurySecurityResponseDto> HandleRegisterSecurityAsync(RegisterTreasurySecurityCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var curr = Currency.FromCode(req.Currency);

        var security = TreasurySecurity.Create(
            securityId: req.SecurityId,
            securityName: req.SecurityName,
            securityType: req.SecurityType,
            classification: req.Classification,
            faceValue: new Money(req.FaceValue, curr),
            bookValue: new Money(req.BookValue, curr),
            couponRate: req.CouponRate,
            purchaseYield: req.PurchaseYield,
            issueDate: req.IssueDate,
            maturityDate: req.MaturityDate,
            branchCode: req.BranchCode);

        await _repository.AddSecurityAsync(security, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapSecurityResponse(security);
    }

    public async Task<MtmRevaluationBatchResponseDto> HandleMtmRevaluationBatchAsync(MtmRevaluationBatchCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var allSecurities = await _repository.GetAllSecuritiesAsync(ct);
        var hftSecurities = allSecurities
            .Where(s => s.Classification == SecurityClassification.Hft)
            .ToList();

        var quotesDict = req.Quotes?.ToDictionary(q => q.SecurityId, StringComparer.OrdinalIgnoreCase)
                         ?? new Dictionary<string, SecurityMarketQuoteDto>(StringComparer.OrdinalIgnoreCase);

        var results = new List<MtmSecurityResultDto>();
        decimal totalNetDiff = 0m;
        int vouchersPosted = 0;
        var today = DateTime.UtcNow.Date;

        foreach (var sec in hftSecurities)
        {
            decimal oldMarketVal = sec.MarketValue.Amount;
            decimal targetYield = sec.CurrentMarketYield;
            decimal targetCleanPrice = sec.CleanPrice;

            if (quotesDict.TryGetValue(sec.SecurityId, out var quote))
            {
                targetYield = quote.MarketYield;
                targetCleanPrice = quote.CleanPrice;
            }

            decimal diff = sec.ExecuteDailyMtmRevaluation(targetYield, targetCleanPrice, today);
            totalNetDiff += diff;

            string? voucherNum = null;
            if (diff != 0m)
            {
                bool isGain = diff > 0m;
                var voucher = await _glService.PostTreasurySecuritiesMtmRevaluationAsync(
                    branchSol: req.BranchCode,
                    securityId: sec.SecurityId,
                    unrealizedGainLossAmount: Math.Abs(diff),
                    isGain: isGain,
                    makerUserId: req.ValuerUserId,
                    checkerUserId: req.ValuerUserId,
                    ct: ct);

                voucherNum = voucher.VoucherNumber;
                vouchersPosted++;
            }

            await _repository.UpdateSecurityAsync(sec, ct);

            results.Add(new MtmSecurityResultDto(
                sec.SecurityId,
                sec.SecurityName,
                oldMarketVal,
                sec.MarketValue.Amount,
                diff,
                voucherNum));
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new MtmRevaluationBatchResponseDto(
            hftSecurities.Count,
            vouchersPosted,
            totalNetDiff,
            results);
    }

    public Task<ParametricVaRResponseDto> HandleEvaluateVarAsync(EvaluateParametricVaRCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var result = MarketRiskCalculator.CalculateParametricVaR(
            req.PortfolioValue,
            req.AnnualVolatilityPercentage,
            req.HoldingDays,
            req.ConfidenceLevel);

        var response = new ParametricVaRResponseDto(
            result.PortfolioValue,
            result.ConfidenceLevel,
            result.HoldingDays,
            result.AnnualVolatilityPercentage,
            result.DailyVolatilityPercentage,
            result.ValueAtRiskAmount,
            result.ValueAtRiskPercentage);

        return Task.FromResult(response);
    }

    public Task<FxNetOpenPositionResponseDto> HandleEvaluateNopAsync(EvaluateFxNetOpenPositionCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var rawTuples = req.Positions.Select(p => (p.Currency, p.LongExposure, p.ShortExposure));
        var result = MarketRiskCalculator.CalculateFxNetOpenPosition(rawTuples, req.ApprovedNopLimit);

        var breakdown = result.Positions.Select(p => new FxCurrencyPositionDto(
            p.Currency,
            p.LongExposure,
            p.ShortExposure,
            p.NetExposure)).ToList();

        var response = new FxNetOpenPositionResponseDto(
            result.TotalNetLongExposure,
            result.TotalNetShortExposure,
            result.OverallNetOpenPosition,
            result.ApprovedNopLimit,
            result.HasNopLimitBreach,
            result.UtilizationPercentage,
            breakdown);

        return Task.FromResult(response);
    }

    public Task<CrrSlrResponse> HandleEvaluateCrrSlrAsync(EvaluateCrrSlrCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var curr = Currency.FromCode(req.Currency);
        var ndtl = new Money(req.NdtlAmount, curr);
        var crrCash = new Money(req.MaintainedCrrCash, curr);
        var slrSecurities = new Money(req.MaintainedSlrSecurities, curr);

        var pos = CrrSlrPosition.Calculate(ndtl, req.BankingType, crrCash, slrSecurities);

        var response = new CrrSlrResponse(
            pos.Ndtl.Amount,
            pos.Ndtl.Currency.Code,
            pos.BankingType.ToString(),
            pos.CrrDailyMinRate,
            pos.ActualCrrPercentage,
            pos.HasCrrShortfall,
            pos.CrrShortfallOrSurplusAmount.Amount,
            pos.SlrRequiredRate,
            pos.ActualSlrPercentage,
            pos.HasSlrShortfall,
            pos.SlrShortfallOrSurplusAmount.Amount);

        return Task.FromResult(response);
    }

    public Task<LiquidityGapResponse> HandleEvaluateLiquidityGapAsync(EvaluateLiquidityGapCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var tupleList = req.Buckets.Select(b => (b.Bucket, b.Inflow, b.Outflow));

        var profile = LiquidityGapProfile.Evaluate(req.Currency, tupleList);

        var bucketOutputs = profile.Buckets.Select(b => new LiquidityBucketOutput(
            b.Bucket.ToString(),
            b.Inflows,
            b.Outflows,
            b.NetGap,
            b.CumulativeGap)).ToList();

        var response = new LiquidityGapResponse(
            profile.Currency,
            bucketOutputs,
            profile.HasShortTermDeficitAlert,
            profile.Summary);

        return Task.FromResult(response);
    }

    private static TreasuryDealResponse MapDealResponse(TreasuryDeal deal) => new(
        deal.DealReference,
        deal.Type.ToString(),
        deal.Direction.ToString(),
        deal.CounterpartyBankBic,
        deal.CounterpartyName,
        deal.PrimaryAmount.Amount,
        deal.PrimaryAmount.Currency.Code,
        deal.SecondaryAmount.Amount,
        deal.SecondaryAmount.Currency.Code,
        deal.ExchangeRate,
        deal.InterestRate,
        deal.DealDate,
        deal.ValueDate,
        deal.Status.ToString(),
        deal.FrontOfficeDealerUserId,
        deal.MidOfficeRiskOfficerUserId,
        deal.BackOfficeSettlementUserId,
        deal.SettledAtUtc,
        deal.BranchCode,
        deal.SettlementVoucherNumber,
        deal.SecurityIsin);

    private static TreasurySecurityResponseDto MapSecurityResponse(TreasurySecurity s) => new(
        s.SecurityId,
        s.SecurityName,
        s.SecurityType.ToString(),
        s.Classification.ToString(),
        s.FaceValue.Amount,
        s.BookValue.Amount,
        s.MarketValue.Amount,
        s.FaceValue.Currency.Code,
        s.CouponRate,
        s.PurchaseYield,
        s.CurrentMarketYield,
        s.CleanPrice,
        s.IssueDate,
        s.MaturityDate,
        s.LastMtmValuationDate,
        s.UnrealizedMtmGainLoss.Amount,
        s.BranchCode);
}
