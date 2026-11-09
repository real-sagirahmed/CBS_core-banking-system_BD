using CBS.Application.Common.Interfaces;
using CBS.Application.Treasury.DTOs;
using CBS.Domain.Treasury;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Treasury.Commands;

public sealed record CaptureTreasuryDealCommand(CaptureTreasuryDealRequest Request);
public sealed record VerifyMidOfficeDealCommand(VerifyMidOfficeDealRequest Request);
public sealed record SettleBackOfficeDealCommand(SettleBackOfficeDealRequest Request);
public sealed record EvaluateCrrSlrCommand(EvaluateCrrSlrRequest Request);
public sealed record EvaluateLiquidityGapCommand(EvaluateLiquidityGapRequest Request);

public class TreasuryCommandHandler
{
    private readonly ITreasuryRepository _repository;

    public TreasuryCommandHandler(ITreasuryRepository repository)
    {
        _repository = repository;
    }

    public async Task<TreasuryDealResponse> HandleCaptureAsync(CaptureTreasuryDealCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var pCurr = Currency.Create(req.PrimaryCurrency, req.PrimaryCurrency, req.PrimaryCurrency);
        var sCurr = Currency.Create(req.SecondaryCurrency, req.SecondaryCurrency, req.SecondaryCurrency);

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
            frontOfficeDealerUserId: req.FrontOfficeDealerUserId);

        await _repository.AddDealAsync(deal, ct);

        return MapResponse(deal);
    }

    public async Task<TreasuryDealResponse> HandleVerifyMidOfficeAsync(VerifyMidOfficeDealCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var deal = await _repository.GetDealByReferenceAsync(req.DealReference, ct)
            ?? throw new KeyNotFoundException($"Treasury Deal '{req.DealReference}' not found.");

        deal.VerifyByMidOffice(req.MidOfficeOfficerUserId);
        await _repository.UpdateDealAsync(deal, ct);

        return MapResponse(deal);
    }

    public async Task<TreasuryDealResponse> HandleSettleBackOfficeAsync(SettleBackOfficeDealCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var deal = await _repository.GetDealByReferenceAsync(req.DealReference, ct)
            ?? throw new KeyNotFoundException($"Treasury Deal '{req.DealReference}' not found.");

        deal.SettleByBackOffice(req.BackOfficeOfficerUserId);
        await _repository.UpdateDealAsync(deal, ct);

        return MapResponse(deal);
    }

    public Task<CrrSlrResponse> HandleEvaluateCrrSlrAsync(EvaluateCrrSlrCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var curr = Currency.Create(req.Currency, req.Currency, req.Currency);
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

    private static TreasuryDealResponse MapResponse(TreasuryDeal deal) => new(
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
        deal.SettledAtUtc);
}
