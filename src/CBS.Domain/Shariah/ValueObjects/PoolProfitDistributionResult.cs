namespace CBS.Domain.Shariah.ValueObjects;

public sealed record PoolProfitDistributionResult
{
    public Guid PoolId { get; init; }
    public decimal GrossIncome { get; init; }
    public decimal MudaribSharePercentage { get; init; }
    public decimal MudaribShareAmount { get; init; }
    public decimal RabUlMalGrossShare { get; init; }
    public decimal PerDeductionAmount { get; init; }
    public decimal IrrDeductionAmount { get; init; }
    public decimal NetDistributableProfit { get; init; }
    public DateTime CalculatedAtUtc { get; init; }

    #pragma warning disable CS8618
    public PoolProfitDistributionResult() { }
    #pragma warning restore CS8618

    public PoolProfitDistributionResult(
        Guid poolId,
        decimal grossIncome,
        decimal mudaribSharePercentage,
        decimal mudaribShareAmount,
        decimal rabUlMalGrossShare,
        decimal perDeductionAmount,
        decimal irrDeductionAmount,
        decimal netDistributableProfit,
        DateTime calculatedAtUtc)
    {
        PoolId = poolId;
        GrossIncome = grossIncome;
        MudaribSharePercentage = mudaribSharePercentage;
        MudaribShareAmount = mudaribShareAmount;
        RabUlMalGrossShare = rabUlMalGrossShare;
        PerDeductionAmount = perDeductionAmount;
        IrrDeductionAmount = irrDeductionAmount;
        NetDistributableProfit = netDistributableProfit;
        CalculatedAtUtc = calculatedAtUtc;
    }
}
