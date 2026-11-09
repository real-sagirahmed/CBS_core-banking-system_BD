using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.UnitTests;

public class AccountNumberTests
{
    [Fact]
    public void Generate_ShouldProduceValid13DigitAccountNumber()
    {
        // Act
        var acc = AccountNumber.Generate(branchSol: "0101", productCode: "001", sequenceNumber: 12345);

        // Assert
        Assert.NotNull(acc);
        Assert.Equal(AccountNumber.RequiredLength, acc.Value.Length);
        Assert.Equal(13, acc.Value.Length);
        Assert.True(acc.Value.All(char.IsDigit));
    }

    [Fact]
    public void From_WithValid13DigitsAndCheckDigit_ShouldSucceed()
    {
        // Arrange
        var generated = AccountNumber.Generate("0001", "101", 1);
        string validNumber = generated.Value;

        // Act
        var acc = AccountNumber.From(validNumber);

        // Assert
        Assert.Equal(validNumber, acc.Value);
    }

    [Theory]
    [InlineData("123456789012")]    // 12 digits (too short)
    [InlineData("12345678901234")]  // 14 digits (too long)
    [InlineData("")]                // Empty
    [InlineData("   ")]             // Whitespace
    public void From_WithInvalidLength_MustThrowInvariantViolationException(string invalidValue)
    {
        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() => AccountNumber.From(invalidValue));
        Assert.Equal("AccountNumberLengthInvariant", ex.InvariantName);
        Assert.Contains("BRPD Circular 12/2024", ex.RegulatoryReference);
    }

    [Fact]
    public void From_WithAlphaCharacters_MustThrowInvariantViolationException()
    {
        // Arrange
        string alphaValue = "010100100012A"; // 13 chars but contains letter

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() => AccountNumber.From(alphaValue));
        Assert.Equal("AccountNumberLengthInvariant", ex.InvariantName);
    }

    [Fact]
    public void From_WithCorruptedCheckDigit_MustThrowInvariantViolationException()
    {
        // Arrange
        var generated = AccountNumber.Generate("0001", "101", 1);
        char originalCheckDigit = generated.Value[12];
        char corruptedCheckDigit = originalCheckDigit == '9' ? '0' : (char)(originalCheckDigit + 1);
        string corruptedNumber = generated.Value[..12] + corruptedCheckDigit;

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() => AccountNumber.From(corruptedNumber));
        Assert.Equal("AccountNumberCheckDigitInvariant", ex.InvariantName);
        Assert.Contains("BRPD Circular 12/2024 (1.1.1-i)", ex.RegulatoryReference);
    }
}
