using CBS.Domain.Enums;

namespace CBS.Domain.ValueObjects;

public sealed record LiquidityBucketItem(
    LiquidityBucket Bucket,
    decimal Inflows,
    decimal Outflows,
    decimal NetGap,
    decimal CumulativeGap);

/// <summary>
/// Structural Liquidity Gap Profile conforming to Bangladesh Bank Asset-Liability Management (ALM) Guidelines
/// and BRPD Circular 12, Clause 6.3.
/// Invariant: Excessive short-term liquidity deficit (> 10% of outflows in 1-14 days) flags prudential alert.
/// </summary>
public sealed record LiquidityGapProfile
{
    public IReadOnlyList<LiquidityBucketItem> Buckets { get; }
    public string Currency { get; }
    public bool HasShortTermDeficitAlert { get; }
    public string Summary { get; }

    private LiquidityGapProfile(IReadOnlyList<LiquidityBucketItem> buckets, string currency, bool hasShortTermDeficitAlert, string summary)
    {
        Buckets = buckets;
        Currency = currency;
        HasShortTermDeficitAlert = hasShortTermDeficitAlert;
        Summary = summary;
    }

    public static LiquidityGapProfile Evaluate(
        string currency,
        IEnumerable<(LiquidityBucket Bucket, decimal Inflow, decimal Outflow)> bucketData)
    {
        var items = new List<LiquidityBucketItem>();
        decimal runningCumulative = 0m;
        bool hasAlert = false;
        string summary = "Normal liquidity profile within tolerance.";

        foreach (var data in bucketData.OrderBy(b => (int)b.Bucket))
        {
            decimal net = data.Inflow - data.Outflow;
            runningCumulative += net;

            var item = new LiquidityBucketItem(data.Bucket, data.Inflow, data.Outflow, net, runningCumulative);
            items.Add(item);

            // BRPD 6.3 & BB ALM Guideline: 1-14 days bucket negative gap limit
            if (data.Bucket == LiquidityBucket.Days1To14 && data.Outflow > 0m)
            {
                var netDeficitRatio = net < 0m ? Math.Abs(net) / data.Outflow : 0m;
                if (netDeficitRatio > 0.10m) // Exceeds 10% tolerance!
                {
                    hasAlert = true;
                    summary = $"Prudential Alert: 1-14 days structural liquidity deficit ({Math.Abs(net):F2} {currency}) exceeds 10% of outflows ({netDeficitRatio:P1}).";
                }
            }
        }

        return new LiquidityGapProfile(items.AsReadOnly(), currency, hasAlert, summary);
    }
}
