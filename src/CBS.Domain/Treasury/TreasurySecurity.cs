using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Treasury;

/// <summary>
/// Government Treasury Securities Aggregate Root (Treasury Bills, Treasury Bonds, Sukuk)
/// conforming to Bangladesh Bank DOS Circular No. 05 (26 May 2014) and BRPD Circular 12 Module 6.
/// Invariants:
/// 1. HFT (Held For Trading) securities are revalued on a daily/weekly Mark-to-Market (MTM) basis with gains/losses to Revaluation Reserve (301020000).
/// 2. HTM (Held To Maturity) securities are held at amortized cost and strictly barred from daily MTM revaluation (DOS Circular 05/2014).
/// 3. Maturity Date must be strictly after Issue Date.
/// </summary>
public sealed class TreasurySecurity : AggregateRoot<string>
{
    public string SecurityId => Id;
    public string SecurityName { get; private set; } = null!;
    public TreasurySecurityType SecurityType { get; private set; }
    public SecurityClassification Classification { get; private set; }
    public Money FaceValue { get; private set; } = null!;
    public Money BookValue { get; private set; } = null!;
    public Money MarketValue { get; private set; } = null!;
    public decimal CouponRate { get; private set; }
    public decimal PurchaseYield { get; private set; }
    public decimal CurrentMarketYield { get; private set; }
    public decimal CleanPrice { get; private set; }
    public DateTime IssueDate { get; private set; }
    public DateTime MaturityDate { get; private set; }
    public DateTime LastMtmValuationDate { get; private set; }
    public Money UnrealizedMtmGainLoss { get; private set; } = null!;
    public string BranchCode { get; private set; } = "001";

    // Parameterless constructor for EF Core ORM materialization
    private TreasurySecurity() : base(string.Empty) { }

    private TreasurySecurity(string securityId) : base(securityId) { }

    public static TreasurySecurity Create(
        string securityId,
        string securityName,
        TreasurySecurityType securityType,
        SecurityClassification classification,
        Money faceValue,
        Money bookValue,
        decimal couponRate,
        decimal purchaseYield,
        DateTime issueDate,
        DateTime maturityDate,
        string branchCode = "001")
    {
        if (string.IsNullOrWhiteSpace(securityId))
            throw new ArgumentException("Security ID / ISIN is required.", nameof(securityId));
        if (string.IsNullOrWhiteSpace(securityName))
            throw new ArgumentException("Security name is required.", nameof(securityName));

        ArgumentNullException.ThrowIfNull(faceValue, nameof(faceValue));
        ArgumentNullException.ThrowIfNull(bookValue, nameof(bookValue));

        if (faceValue.Amount <= 0m)
            throw new ArgumentException("Face value must be strictly positive.", nameof(faceValue));
        if (bookValue.Amount <= 0m)
            throw new ArgumentException("Book value must be strictly positive.", nameof(bookValue));

        if (maturityDate.Date <= issueDate.Date)
            throw new InvalidOperationException("Regulatory Invariant Violation: Security maturity date must be after issue date.");

        var currency = faceValue.Currency;
        var initialMarketValue = new Money(bookValue.Amount, currency);
        var zeroGainLoss = new Money(0m, currency);
        decimal initialCleanPrice = Math.Round((bookValue.Amount / faceValue.Amount) * 100m, 4);

        return new TreasurySecurity(securityId.Trim().ToUpperInvariant())
        {
            SecurityName = securityName.Trim(),
            SecurityType = securityType,
            Classification = classification,
            FaceValue = faceValue,
            BookValue = bookValue,
            MarketValue = initialMarketValue,
            CouponRate = couponRate,
            PurchaseYield = purchaseYield,
            CurrentMarketYield = purchaseYield,
            CleanPrice = initialCleanPrice,
            IssueDate = issueDate.Date,
            MaturityDate = maturityDate.Date,
            LastMtmValuationDate = issueDate.Date,
            UnrealizedMtmGainLoss = zeroGainLoss,
            BranchCode = string.IsNullOrWhiteSpace(branchCode) ? "001" : branchCode.Trim()
        };
    }

    /// <summary>
    /// Executes daily Mark-to-Market (MTM) revaluation for Held-For-Trading (HFT) securities.
    /// Invariant: HTM securities are strictly barred from daily MTM revaluation (DOS Circular 05/2014).
    /// </summary>
    /// <returns>The net MTM valuation differential to post to GL Revaluation Reserve.</returns>
    public decimal ExecuteDailyMtmRevaluation(decimal currentMarketYield, decimal cleanPrice, DateTime valuationDate)
    {
        if (Classification == SecurityClassification.Htm)
        {
            throw new InvalidOperationException("Regulatory Invariant Violation [DOS Circular 05/2014]: HTM (Held-To-Maturity) securities are carried at amortized cost and strictly exempt from daily Mark-to-Market revaluation.");
        }

        if (cleanPrice <= 0m)
            throw new ArgumentException("Clean price must be strictly positive.", nameof(cleanPrice));

        decimal oldMarketValueAmount = MarketValue.Amount;
        decimal newMarketValueAmount = Math.Round(FaceValue.Amount * (cleanPrice / 100m), 2);
        decimal unrealizedAmount = newMarketValueAmount - BookValue.Amount;

        MarketValue = new Money(newMarketValueAmount, FaceValue.Currency);
        UnrealizedMtmGainLoss = new Money(unrealizedAmount, FaceValue.Currency);
        CurrentMarketYield = currentMarketYield;
        CleanPrice = cleanPrice;
        LastMtmValuationDate = valuationDate.Date;

        // Differential from previous market valuation
        return newMarketValueAmount - oldMarketValueAmount;
    }
}
