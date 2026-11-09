using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.UnitTests;

public class MoneyTests
{
    [Fact]
    public void Addition_WithSameCurrency_ShouldSucceed()
    {
        // Arrange
        var m1 = new Money(100.50m, Currency.BDT);
        var m2 = new Money(50.25m, Currency.BDT);

        // Act
        var result = m1 + m2;

        // Assert
        Assert.Equal(150.75m, result.Amount);
        Assert.Equal(Currency.BDT, result.Currency);
    }

    [Fact]
    public void Addition_WithDifferentCurrencies_MustThrowInvariantViolationException()
    {
        // Arrange
        var bdt = new Money(100m, Currency.BDT);
        var usd = new Money(100m, Currency.USD);

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() => _ = bdt + usd);
        Assert.Equal("CurrencyMismatch", ex.InvariantName);
        Assert.Contains("BRPD 1.3.2", ex.RegulatoryReference);
    }

    [Fact]
    public void Rounding_MustRoundToCurrencyDecimalPlaces()
    {
        // Arrange & Act
        var money = new Money(100.555m, Currency.BDT); // 2 decimals

        // Assert
        Assert.Equal(100.56m, money.Amount);
    }
}
