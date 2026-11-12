using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.TradeFinance;

// ARCHITECTURAL INTENT: Uniform Template for Price Verification Aggregate Root
// maintaining benchmark reference prices across commodities and HS codes to detect Over-invoicing
// (capital flight) and Under-invoicing (illicit funds retention) per Bangladesh Bank BRPD Circular 12 (Module 5.7.6).
public sealed class TbmlPriceBenchmark : AggregateRoot<Guid>
{
    public string HsCode { get; private set; } = null!;
    public string ItemDescription { get; private set; } = null!;
    public string UnitOfMeasure { get; private set; } = null!;
    public decimal BenchmarkPriceMin { get; private set; }
    public decimal BenchmarkPriceMax { get; private set; }
    public Currency Currency { get; private set; } = null!;
    public DateOnly EffectiveDate { get; private set; }
    public string SourceReference { get; private set; } = null!;

    #pragma warning disable CS8618
    private TbmlPriceBenchmark() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public TbmlPriceBenchmark(
        Guid id,
        string hsCode,
        string itemDescription,
        string unitOfMeasure,
        decimal benchmarkPriceMin,
        decimal benchmarkPriceMax,
        Currency currency,
        DateOnly effectiveDate,
        string sourceReference)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(hsCode))
            throw new ArgumentException("HS Code is required.", nameof(hsCode));

        if (benchmarkPriceMin <= 0m || benchmarkPriceMax < benchmarkPriceMin)
            throw new ArgumentException("Invalid benchmark price range.", nameof(benchmarkPriceMin));

        ArgumentNullException.ThrowIfNull(currency, nameof(currency));

        HsCode = hsCode.Trim();
        ItemDescription = itemDescription.Trim();
        UnitOfMeasure = unitOfMeasure.Trim().ToUpperInvariant();
        BenchmarkPriceMin = benchmarkPriceMin;
        BenchmarkPriceMax = benchmarkPriceMax;
        Currency = currency;
        EffectiveDate = effectiveDate;
        SourceReference = sourceReference.Trim();
    }

    public bool CheckPriceVariance(
        decimal declaredUnitPrice,
        bool isExport,
        out decimal variancePercentage,
        out TbmlAlertType? alertType,
        out string? narrative)
    {
        variancePercentage = 0m;
        alertType = null;
        narrative = null;

        if (!isExport) // Import: Test for Over-Invoicing
        {
            if (declaredUnitPrice > BenchmarkPriceMax)
            {
                variancePercentage = Math.Round(((declaredUnitPrice - BenchmarkPriceMax) / BenchmarkPriceMax) * 100m, 2);
                if (variancePercentage > 20.00m)
                {
                    alertType = TbmlAlertType.PriceVarianceOverInvoicing;
                    narrative = $"Import unit price ({declaredUnitPrice:N2} {Currency.Code}/{UnitOfMeasure}) exceeds benchmark ceiling ({BenchmarkPriceMax:N2}) by {variancePercentage}%. Suspected capital flight.";
                    return true;
                }
            }
        }
        else // Export: Test for Under-Invoicing
        {
            if (declaredUnitPrice < BenchmarkPriceMin)
            {
                variancePercentage = Math.Round(((BenchmarkPriceMin - declaredUnitPrice) / BenchmarkPriceMin) * -100m, 2);
                if (variancePercentage < -20.00m)
                {
                    alertType = TbmlAlertType.PriceVarianceUnderInvoicing;
                    narrative = $"Export unit price ({declaredUnitPrice:N2} {Currency.Code}/{UnitOfMeasure}) is below benchmark floor ({BenchmarkPriceMin:N2}) by {Math.Abs(variancePercentage)}%. Suspected illicit funds retention.";
                    return true;
                }
            }
        }

        return false;
    }
}
