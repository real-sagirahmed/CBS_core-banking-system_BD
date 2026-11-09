using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.GeneralLedger;

// ARCHITECTURAL INTENT: Multi-Currency revaluation and dual-balance maintenance (BRPD 1.3.2, 1.3.9).
public sealed record ExchangeRate
{
    public Currency BaseCurrency { get; }
    public Currency ForeignCurrency { get; }
    public decimal MidRate { get; }
    public decimal BuyRate { get; }
    public decimal SellRate { get; }
    public DateOnly RateDate { get; }

    public ExchangeRate(
        Currency foreignCurrency,
        decimal midRate,
        decimal buyRate,
        decimal sellRate,
        DateOnly rateDate)
    {
        ArgumentNullException.ThrowIfNull(foreignCurrency, nameof(foreignCurrency));
        if (foreignCurrency.IsBaseCurrency)
            throw new ArgumentException("Foreign currency cannot be the base currency (BDT).", nameof(foreignCurrency));
        if (midRate <= 0m || buyRate <= 0m || sellRate <= 0m)
            throw new ArgumentException("Exchange rates must be greater than zero.");
        if (buyRate > sellRate)
            throw new ArgumentException($"Buy rate ({buyRate}) cannot be greater than Sell rate ({sellRate}).");

        BaseCurrency = Currency.BDT;
        ForeignCurrency = foreignCurrency;
        MidRate = midRate;
        BuyRate = buyRate;
        SellRate = sellRate;
        RateDate = rateDate;
    }

    public Money ConvertToBdt(Money foreignMoney)
    {
        if (foreignMoney.Currency != ForeignCurrency)
        {
            throw new InvariantViolationException(
                "CurrencyMismatchInvariant",
                "BRPD 1.3.2",
                $"Cannot convert money in currency '{foreignMoney.Currency.Code}' using rate for '{ForeignCurrency.Code}'.");
        }

        decimal bdtAmount = decimal.Round(foreignMoney.Amount * MidRate, Currency.BDT.DecimalPlaces, MidpointRounding.AwayFromZero);
        return Money.Bdt(bdtAmount);
    }

    public Money CalculateRevaluationDelta(Money foreignMoney, Money currentBookedBdt)
    {
        var markToMarketBdt = ConvertToBdt(foreignMoney);
        return markToMarketBdt - currentBookedBdt;
    }
}
