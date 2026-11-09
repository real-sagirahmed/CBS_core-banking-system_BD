using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.UnitTests;

public class DepositAccountTests
{
    private static AccountNumber CreateValidAccountNumber() => AccountNumber.Generate("001", "120", 1);
    private static CifNumber CreateValidCif() => new("CIF100000001");

    [Fact]
    public void Credit_WithValidAmount_ShouldIncreaseBalanceAndTrackHighestCredit()
    {
        var acc = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.SavingsAccount,
            "001",
            new DateOnly(2025, 1, 1),
            Currency.BDT);

        acc.Credit(Money.Bdt(50000m), "Opening Cash Deposit");
        Assert.Equal(50000m, acc.Balance.Amount);
        Assert.Equal(50000m, acc.HighestCreditBalanceInYear.Amount);

        acc.Credit(Money.Bdt(20000m), "Second Deposit");
        Assert.Equal(70000m, acc.Balance.Amount);
        Assert.Equal(70000m, acc.HighestCreditBalanceInYear.Amount);
    }

    [Fact]
    public void Debit_WithSufficientBalance_ShouldDecreaseBalance()
    {
        var acc = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.SavingsAccount,
            "001",
            new DateOnly(2025, 1, 1),
            Currency.BDT);

        acc.Credit(Money.Bdt(100000m), "Deposit");
        acc.Debit(Money.Bdt(40000m), "Cash Withdrawal");

        Assert.Equal(60000m, acc.Balance.Amount);
        Assert.Equal(100000m, acc.HighestCreditBalanceInYear.Amount); // Highest credit preserved
    }

    [Fact]
    public void Debit_WhenInsufficientBalance_ShouldThrowInsufficientFundsInvariant()
    {
        // BRPD Circular 12/2024 Section 1.3.5: No unauthorized overdraft
        var acc = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.SavingsAccount,
            "001",
            new DateOnly(2025, 1, 1),
            Currency.BDT);

        acc.Credit(Money.Bdt(10000m), "Deposit");

        var ex = Assert.Throws<InvariantViolationException>(() =>
            acc.Debit(Money.Bdt(15000m), "Overdraft Attempt"));

        Assert.Equal("InsufficientFundsInvariant", ex.InvariantName);
    }

    [Fact]
    public void EvaluateAndApplyDormancy_WhenCurrentAccountInactiveFor6Months_ShouldTransitionToDormant()
    {
        // BRPD Circular 12/2024 Section 3.1.20: Current account becomes dormant after 6 months
        var openedDate = new DateOnly(2025, 1, 1);
        var acc = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.CurrentAccount,
            "001",
            openedDate,
            Currency.BDT);

        var dateAfter5Months = new DateOnly(2025, 6, 1);
        Assert.False(acc.EvaluateAndApplyDormancy(dateAfter5Months));
        Assert.Equal(AccountOperatingStatus.Active, acc.Status);

        var dateAfter6Months = new DateOnly(2025, 7, 1);
        Assert.True(acc.EvaluateAndApplyDormancy(dateAfter6Months));
        Assert.Equal(AccountOperatingStatus.Dormant, acc.Status);
        Assert.Equal(dateAfter6Months, acc.DormantDate);
    }

    [Fact]
    public void EvaluateAndApplyDormancy_WhenSavingsAccountInactiveFor12Months_ShouldTransitionToDormant()
    {
        // BRPD Circular 12/2024 Section 3.1.20: Savings account becomes dormant after 12 months
        var openedDate = new DateOnly(2025, 1, 1);
        var acc = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.SavingsAccount,
            "001",
            openedDate,
            Currency.BDT);

        var dateAfter11Months = new DateOnly(2025, 12, 1);
        Assert.False(acc.EvaluateAndApplyDormancy(dateAfter11Months));

        var dateAfter12Months = new DateOnly(2026, 1, 1);
        Assert.True(acc.EvaluateAndApplyDormancy(dateAfter12Months));
        Assert.Equal(AccountOperatingStatus.Dormant, acc.Status);
    }

    [Fact]
    public void Debit_WhenAccountIsDormant_ShouldThrowDormantAccountDebitBlockedInvariant()
    {
        // BRPD Circular 12/2024 Section 3.1.20: Hard lock on customer-induced debits on dormant accounts
        var openedDate = new DateOnly(2025, 1, 1);
        var acc = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.SavingsAccount,
            "001",
            openedDate,
            Currency.BDT);

        acc.Credit(Money.Bdt(50000m), "Initial Deposit", isCustomerInduced: true, transactionDate: openedDate);
        acc.EvaluateAndApplyDormancy(new DateOnly(2026, 2, 1)); // Transition to Dormant

        var ex = Assert.Throws<InvariantViolationException>(() =>
            acc.Debit(Money.Bdt(5000m), "ATM Withdrawal Attempt"));

        Assert.Equal("DormantAccountDebitBlockedInvariant", ex.InvariantName);
    }

    [Fact]
    public void Credit_WhenAccountIsDormant_ShouldSucceed()
    {
        // Credits (e.g. inward remittances, interest) are permitted on dormant accounts
        var openedDate = new DateOnly(2025, 1, 1);
        var acc = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.SavingsAccount,
            "001",
            openedDate,
            Currency.BDT);

        acc.EvaluateAndApplyDormancy(new DateOnly(2026, 2, 1));

        acc.Credit(Money.Bdt(10000m), "Inward Clearing Credit", isCustomerInduced: false);
        Assert.Equal(10000m, acc.Balance.Amount);
    }

    [Fact]
    public void ReactivateDormant_WithBranchManagerApproval_ShouldTransitionToActive()
    {
        // BRPD Circular 12/2024 Section 3.1.20-iv
        var acc = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.SavingsAccount,
            "001",
            new DateOnly(2025, 1, 1),
            Currency.BDT);

        acc.EvaluateAndApplyDormancy(new DateOnly(2026, 2, 1));
        Assert.Equal(AccountOperatingStatus.Dormant, acc.Status);

        acc.ReactivateDormant("BM_OFFICER_01", "Customer appeared in branch with valid Smart NID and written request.");

        Assert.Equal(AccountOperatingStatus.Active, acc.Status);
        Assert.Null(acc.DormantDate);
        Assert.Equal("BM_OFFICER_01", acc.ReactivatedByUserId);
        Assert.NotNull(acc.ReactivatedAtUtc);
    }

    [Fact]
    public void CalculateWithholdingTax_ShouldDifferentiateBetweenTinAndNonTin()
    {
        // BRPD Circular 12/2024 Section 3.1.19-i: 10% with TIN, 15% without TIN
        var accWithTin = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.SavingsAccount,
            "001",
            new DateOnly(2025, 1, 1),
            Currency.BDT,
            hasTin: true);

        var accWithoutTin = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.SavingsAccount,
            "001",
            new DateOnly(2025, 1, 1),
            Currency.BDT,
            hasTin: false);

        var interest = Money.Bdt(10000m);

        var taxWithTin = accWithTin.CalculateWithholdingTax(interest);
        var taxWithoutTin = accWithoutTin.CalculateWithholdingTax(interest);

        Assert.Equal(1000m, taxWithTin.Amount);   // 10%
        Assert.Equal(1500m, taxWithoutTin.Amount); // 15%
    }

    [Theory]
    [InlineData(50000, 0)]          // Up to 100K -> Nil
    [InlineData(100000, 0)]         // Up to 100K -> Nil
    [InlineData(250000, 150)]       // 100K - 500K -> 150 BDT
    [InlineData(750000, 500)]       // 500K - 1M -> 500 BDT
    [InlineData(5000000, 3000)]     // 1M - 10M -> 3,000 BDT
    [InlineData(25000000, 15000)]   // 10M - 50M -> 15,000 BDT
    [InlineData(100000000, 50000)]  // Above 50M -> 50,000 BDT
    public void CalculateAnnualExciseDuty_ShouldMatchNBRStatutorySlabs(decimal balance, decimal expectedDuty)
    {
        // BRPD Circular 12/2024 Section 3.1.19-vii: NBR Excise Duty Slabs
        var acc = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.SavingsAccount,
            "001",
            new DateOnly(2025, 1, 1),
            Currency.BDT);

        acc.Credit(Money.Bdt(balance), "Deposit");

        var duty = acc.CalculateAnnualExciseDuty();
        Assert.Equal(expectedDuty, duty.Amount);
    }

    [Fact]
    public void Credit_WhenHoveringChargesExist_ShouldAutoRecoverHoveringCharges()
    {
        // BRPD Circular 12/2024 Section 3.1.19-viii: Auto-recovery of hovering charges
        var acc = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.SavingsAccount,
            "001",
            new DateOnly(2025, 1, 1),
            Currency.BDT);

        // Account has zero balance, but 575 BDT annual fee is assessed
        acc.AddHoveringCharge(Money.Bdt(575m));
        Assert.Equal(575m, acc.HoveringCharges.Amount);
        Assert.Equal(0m, acc.Balance.Amount);

        // Customer deposits 1,000 BDT
        var recovered = acc.Credit(Money.Bdt(1000m), "Cash Deposit");

        Assert.Equal(575m, recovered.Amount); // 575 BDT recovered
        Assert.Equal(0m, acc.HoveringCharges.Amount); // Hovering cleared
        Assert.Equal(425m, acc.Balance.Amount); // 1000 - 575 = 425 net balance
    }

    [Fact]
    public void Evaluate10YearUnclaimedTransfer_WhenInactiveFor120Months_ShouldTransitionToUnclaimed()
    {
        // BRPD Circular 12/2024 Section 3.1.21-iv: 10-year transfer to Bangladesh Bank
        var openedDate = new DateOnly(2015, 1, 1);
        var acc = new DepositAccount(
            CreateValidAccountNumber(),
            CreateValidCif(),
            DepositProductType.SavingsAccount,
            "001",
            openedDate,
            Currency.BDT);

        var dateAfter9Years = new DateOnly(2024, 1, 1);
        Assert.False(acc.Evaluate10YearUnclaimedTransfer(dateAfter9Years));

        var dateAfter10Years = new DateOnly(2025, 1, 1);
        Assert.True(acc.Evaluate10YearUnclaimedTransfer(dateAfter10Years));
        Assert.Equal(AccountOperatingStatus.Unclaimed10YearTransferred, acc.Status);
    }
}
