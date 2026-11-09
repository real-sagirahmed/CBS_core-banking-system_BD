using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.ValueObjects;

// ARCHITECTURAL INTENT: Collateral Asset Valuation & Drawing Power (DP) Calculation
// according to Bangladesh Bank BRPD Circular 12/2024 (Module 4.1.3, 4.1.6).
public sealed record CollateralAsset
{
    public string CollateralId { get; }
    public CollateralType Type { get; }
    public Money MarketValue { get; }
    public Money ForcedSaleValue { get; }
    public decimal MarginPercentage { get; }

    public CollateralAsset(
        string collateralId,
        CollateralType type,
        Money marketValue,
        Money forcedSaleValue,
        decimal marginPercentage)
    {
        if (string.IsNullOrWhiteSpace(collateralId))
            throw new ArgumentException("Collateral ID is required.", nameof(collateralId));
        ArgumentNullException.ThrowIfNull(marketValue, nameof(marketValue));
        ArgumentNullException.ThrowIfNull(forcedSaleValue, nameof(forcedSaleValue));

        if (marginPercentage < 0 || marginPercentage > 100)
        {
            throw new InvariantViolationException(
                "InvalidCollateralMarginInvariant",
                "BRPD Circular 12/2024 (4.1.6)",
                $"Collateral haircut/margin percentage must be between 0 and 100 (provided: {marginPercentage}%).");
        }

        if (forcedSaleValue > marketValue)
        {
            throw new InvariantViolationException(
                "ForcedSaleValueExceedsMarketValueInvariant",
                "BRPD Circular 12/2024 (4.1.3)",
                $"Forced Sale Value ({forcedSaleValue}) cannot exceed Market Value ({marketValue}).");
        }

        CollateralId = collateralId.Trim();
        Type = type;
        MarketValue = marketValue;
        ForcedSaleValue = forcedSaleValue;
        MarginPercentage = marginPercentage;
    }

    public Money CalculateEligibleDrawingPower()
    {
        decimal factor = 1m - (MarginPercentage / 100m);
        decimal dpAmount = Math.Round(ForcedSaleValue.Amount * factor, 2, MidpointRounding.AwayFromZero);
        return Money.Bdt(dpAmount);
    }
}
