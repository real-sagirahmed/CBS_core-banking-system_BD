using CBS.Domain.Enums;

namespace CBS.Domain.Reporting.ValueObjects;

public sealed record DepositSlabSummaryRow
{
    public DepositSlabRange Slab { get; init; }
    public string SlabLabel { get; init; }
    public int AccountCount { get; init; }
    public decimal TotalBalance { get; init; }
    public decimal PercentageOfTotalDeposit { get; init; }

    #pragma warning disable CS8618
    public DepositSlabSummaryRow() { }
    #pragma warning restore CS8618

    public DepositSlabSummaryRow(
        DepositSlabRange slab,
        string slabLabel,
        int accountCount,
        decimal totalBalance,
        decimal percentageOfTotalDeposit)
    {
        Slab = slab;
        SlabLabel = slabLabel ?? throw new ArgumentNullException(nameof(slabLabel));
        AccountCount = accountCount;
        TotalBalance = totalBalance;
        PercentageOfTotalDeposit = Math.Round(percentageOfTotalDeposit, 2, MidpointRounding.AwayFromZero);
    }
}
