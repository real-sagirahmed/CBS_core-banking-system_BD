// ARCHITECTURAL INTENT: FX Currency Blotter Position Aggregate Root
// Tracking currency-wise intraday bought/sold volumes, weighted average rates, and Net Open Position (NOP)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 6.2.1-ii, 6.2.2-i, 6.5.9.

using CBS.Domain.Common;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Treasury;

public sealed class FxBlotterPosition : AggregateRoot<Guid>
{
    public string Currency { get; private set; } = null!;
    public DateOnly PositionDate { get; private set; }
    public decimal OpeningPosition { get; private set; }
    public decimal TotalBoughtFcy { get; private set; }
    public decimal TotalSoldFcy { get; private set; }
    public decimal TotalCostBdt { get; private set; }
    public decimal TotalProceedsBdt { get; private set; }
    public decimal RevaluationMidRate { get; private set; }
    public DateTime LastUpdatedAtUtc { get; private set; }

    public decimal NetPosition => OpeningPosition + TotalBoughtFcy - TotalSoldFcy;
    public decimal WeightedAverageBuyRate => TotalBoughtFcy > 0 ? Math.Round(TotalCostBdt / TotalBoughtFcy, 4) : 0m;
    public decimal WeightedAverageSellRate => TotalSoldFcy > 0 ? Math.Round(TotalProceedsBdt / TotalSoldFcy, 4) : 0m;
    public decimal BdtEquivalent => Math.Round(NetPosition * RevaluationMidRate, 2);

    #pragma warning disable CS8618
    private FxBlotterPosition() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public FxBlotterPosition(
        Guid id,
        string currency,
        DateOnly positionDate,
        decimal openingPosition,
        decimal initialMidRate)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency code is required.", nameof(currency));
        if (initialMidRate <= 0)
            throw new ArgumentException("Initial mid rate must be strictly positive.", nameof(initialMidRate));

        Currency = currency.Trim().ToUpperInvariant();
        PositionDate = positionDate;
        OpeningPosition = openingPosition;
        TotalBoughtFcy = 0m;
        TotalSoldFcy = 0m;
        TotalCostBdt = 0m;
        TotalProceedsBdt = 0m;
        RevaluationMidRate = initialMidRate;
        LastUpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordBuy(decimal fcyAmount, decimal exchangeRate)
    {
        if (fcyAmount <= 0) throw new ArgumentException("FCY amount must be positive.", nameof(fcyAmount));
        if (exchangeRate <= 0) throw new ArgumentException("Exchange rate must be positive.", nameof(exchangeRate));

        TotalBoughtFcy += fcyAmount;
        TotalCostBdt += Math.Round(fcyAmount * exchangeRate, 2);
        LastUpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordSell(decimal fcyAmount, decimal exchangeRate)
    {
        if (fcyAmount <= 0) throw new ArgumentException("FCY amount must be positive.", nameof(fcyAmount));
        if (exchangeRate <= 0) throw new ArgumentException("Exchange rate must be positive.", nameof(exchangeRate));

        TotalSoldFcy += fcyAmount;
        TotalProceedsBdt += Math.Round(fcyAmount * exchangeRate, 2);
        LastUpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateMidRate(decimal midRate)
    {
        if (midRate <= 0) throw new ArgumentException("Mid rate must be positive.", nameof(midRate));
        RevaluationMidRate = midRate;
        LastUpdatedAtUtc = DateTime.UtcNow;
    }
}
