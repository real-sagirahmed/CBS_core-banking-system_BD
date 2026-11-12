using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.UnitTests;

public class AccountClosingTests
{
    private static DepositAccount CreateTestAccount(decimal initialBalance = 10_000m)
    {
        var accNo = AccountNumber.Generate("0101", "001", 201);
        var cif = new CifNumber("CIF-99999");
        var account = new DepositAccount(
            accNo,
            cif,
            DepositProductType.SavingsAccount,
            "0101",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
            Currency.BDT,
            hasTin: true);

        if (initialBalance > 0m)
        {
            account.Credit(Money.Bdt(initialBalance), "Initial Deposit", isCustomerInduced: true);
        }

        return account;
    }

    [Fact]
    public void CloseAccount_WithAccruedInterestProvision_ThrowsInvariantViolation()
    {
        // Arrange
        var account = CreateTestAccount(50_000m);
        account.AccrueDailyInterest(DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.True(account.AccruedInterestProvision.Amount > 0m);

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() =>
            account.CloseAccount(
                "MAKER01",
                "CHECKER01",
                "Customer Relocation",
                DateOnly.FromDateTime(DateTime.UtcNow),
                out _, out _, out _));

        Assert.Equal("UnappliedInterestProvisionInvariant", ex.InvariantName);
        Assert.Contains("BRPD Circular 12/2024 (1.6.4-iv)", ex.RegulatoryReference);
    }

    [Fact]
    public void CloseAccount_WithActiveRestraintHold_ThrowsInvariantViolation()
    {
        // Arrange
        var account = CreateTestAccount(50_000m);
        account.ApplyHold(Money.Bdt(10_000m));

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() =>
            account.CloseAccount(
                "MAKER01",
                "CHECKER01",
                "Closure Request",
                DateOnly.FromDateTime(DateTime.UtcNow),
                out _, out _, out _));

        Assert.Equal("ActiveRestraintsHoldInvariant", ex.InvariantName);
        Assert.Contains("BRPD Circular 12/2024 (1.6.2-v, 3.1.15-i)", ex.RegulatoryReference);
    }

    [Fact]
    public void CloseAccount_MakerCheckerSameUser_ThrowsInvariantViolation()
    {
        // Arrange
        var account = CreateTestAccount(50_000m);

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() =>
            account.CloseAccount(
                "MAKER01",
                "MAKER01",
                "Self-approval attempt",
                DateOnly.FromDateTime(DateTime.UtcNow),
                out _, out _, out _));

        Assert.Equal("MakerCheckerSeparationInvariant", ex.InvariantName);
    }

    [Fact]
    public void CloseAccount_SufficientBalance_DeductsFeeAndVatAndZerosBalance()
    {
        // Arrange
        var account = CreateTestAccount(10_000m);

        // Act
        account.CloseAccount(
            "MAKER01",
            "CHECKER01",
            "Customer Migration",
            DateOnly.FromDateTime(DateTime.UtcNow),
            out var fee,
            out var vat,
            out var netRefund);

        // Assert: Fee = 300, VAT = 45, Net Refund = 10000 - 345 = 9655
        Assert.Equal(300.00m, fee.Amount);
        Assert.Equal(45.00m, vat.Amount);
        Assert.Equal(9_655.00m, netRefund.Amount);
        Assert.Equal(0m, account.Balance.Amount);
        Assert.Equal(AccountOperatingStatus.Closed, account.Status);
        Assert.Equal("MAKER01", account.ClosedByUserId);
        Assert.Equal("CHECKER01", account.ClosedAuthorizedByUserId);
        Assert.Equal("Customer Migration", account.ClosingReason);
    }

    [Fact]
    public void CloseAccount_InsufficientBalanceForFee_DeductsEntireBalanceAndZeros()
    {
        // Arrange
        var account = CreateTestAccount(200.00m); // Balance less than 345 BDT

        // Act
        account.CloseAccount(
            "MAKER01",
            "CHECKER01",
            "Low balance closure",
            DateOnly.FromDateTime(DateTime.UtcNow),
            out var fee,
            out var vat,
            out var netRefund);

        // Assert
        Assert.Equal(200.00m, fee.Amount);
        Assert.Equal(0m, vat.Amount);
        Assert.Equal(0m, netRefund.Amount);
        Assert.Equal(0m, account.Balance.Amount);
        Assert.Equal(AccountOperatingStatus.Closed, account.Status);
    }

    [Fact]
    public void CloseAccount_AlreadyClosed_ThrowsInvalidOperationException()
    {
        // Arrange
        var account = CreateTestAccount(5_000m);
        account.CloseAccount("MAKER01", "CHECKER01", "Initial close", DateOnly.FromDateTime(DateTime.UtcNow), out _, out _, out _);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            account.CloseAccount("MAKER01", "CHECKER01", "Duplicate close", DateOnly.FromDateTime(DateTime.UtcNow), out _, out _, out _));
    }
}
