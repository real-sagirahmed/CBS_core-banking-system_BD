namespace CBS.Domain.Reporting.ValueObjects;

public sealed record DepositMixSummaryRow
{
    public string CategoryDimension { get; init; } // e.g. "ProductCostType", "CustomerSegment", "GeographicArea", "Gender"
    public string ClassificationGroup { get; init; } // e.g. "LowCost_CASA", "HighCost_Term", "Retail", "Corporate", "CMSME", "Rural", "Urban"
    public int AccountCount { get; init; }
    public decimal TotalDepositAmount { get; init; }
    public decimal PercentageOfTotal { get; init; }
    public decimal WeightedAverageRate { get; init; }

    #pragma warning disable CS8618
    public DepositMixSummaryRow() { }
    #pragma warning restore CS8618

    public DepositMixSummaryRow(
        string categoryDimension,
        string classificationGroup,
        int accountCount,
        decimal totalDepositAmount,
        decimal percentageOfTotal,
        decimal weightedAverageRate)
    {
        CategoryDimension = categoryDimension ?? throw new ArgumentNullException(nameof(categoryDimension));
        ClassificationGroup = classificationGroup ?? throw new ArgumentNullException(nameof(classificationGroup));
        AccountCount = accountCount;
        TotalDepositAmount = totalDepositAmount;
        PercentageOfTotal = Math.Round(percentageOfTotal, 2, MidpointRounding.AwayFromZero);
        WeightedAverageRate = Math.Round(weightedAverageRate, 2, MidpointRounding.AwayFromZero);
    }
}
