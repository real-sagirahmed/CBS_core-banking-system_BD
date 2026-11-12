using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.UnitTests;

public class AccountRestraintAndLienTests
{
    private static DepositAccount CreateTestAccount(decimal initialBalance = 100_000m)
    {
        var accNo = AccountNumber.Generate("0101", "001", 101);
        var cif = new CifNumber("CIF-12345");
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
    public void PlaceRestraint_CourtAttachment_FreezesAccount_And_RejectsDebitsAndCredits()
    {
        // Arrange
        var account = CreateTestAccount(100_000m);
        var restraint = new AccountRestraint(
            "RST-0001",
            account.Id,
            RestraintType.CourtAttachmentOrder,
            RestraintScope.TotalFreeze,
            Money.Zero(Currency.BDT),
            "CourtWrit",
            "High Court Division Writ Petition No. 1234/2026",
            "Supreme Court of Bangladesh",
            "WP-1234/2026",
            "MAKER01",
            "CHECKER01",
            DateTime.UtcNow);

        // Act
        account.SetOperatingStatus(AccountOperatingStatus.Frozen);

        // Assert
        Assert.Equal(AccountOperatingStatus.Frozen, account.Status);
        Assert.Equal(RestraintStatus.Active, restraint.Status);

        var debitEx = Assert.Throws<InvariantViolationException>(() =>
            account.Debit(Money.Bdt(1000m), "Withdrawal Attempt"));
        Assert.Equal("AccountDebitBlockedInvariant", debitEx.InvariantName);

        var creditEx = Assert.Throws<InvariantViolationException>(() =>
            account.Credit(Money.Bdt(1000m), "Deposit Attempt"));
        Assert.Equal("AccountCreditBlockedInvariant", creditEx.InvariantName);
    }

    [Fact]
    public void PlaceRestraint_PartialHold_EnforcesAvailableBalance_And_RejectsBreachingDebits()
    {
        // Arrange
        var account = CreateTestAccount(100_000m);
        var holdMoney = Money.Bdt(40_000m);

        // Act
        account.ApplyHold(holdMoney);

        // Assert
        Assert.Equal(100_000m, account.Balance.Amount);
        Assert.Equal(40_000m, account.TotalActiveHoldAmount.Amount);
        Assert.Equal(60_000m, account.AvailableBalance.Amount);

        // Within available balance (50,000 <= 60,000) -> Succeeds
        account.Debit(Money.Bdt(50_000m), "Permissible Debit");
        Assert.Equal(50_000m, account.Balance.Amount);
        Assert.Equal(10_000m, account.AvailableBalance.Amount);

        // Breaches available balance (20,000 > 10,000) even though total balance is 50,000 -> Throws invariant!
        var ex = Assert.Throws<InvariantViolationException>(() =>
            account.Debit(Money.Bdt(20_000m), "Encroaching Debit"));

        Assert.Equal("InsufficientAvailableBalanceAfterLienInvariant", ex.InvariantName);
        Assert.Contains("BRPD Circular 12/2024 (1.6.6, 3.1.15-ix, x)", ex.RegulatoryReference);
    }

    [Fact]
    public void DebitBlocked_BlocksDebit_AllowsCredit()
    {
        // Arrange
        var account = CreateTestAccount(50_000m);
        account.SetOperatingStatus(AccountOperatingStatus.BlockedDebit);

        // Act & Assert
        account.Credit(Money.Bdt(10_000m), "Inward Remittance Credit");
        Assert.Equal(60_000m, account.Balance.Amount);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            account.Debit(Money.Bdt(5_000m), "Debit Attempt"));
        Assert.Equal("AccountDebitBlockedInvariant", ex.InvariantName);
    }

    [Fact]
    public void CreditBlocked_BlocksCredit_AllowsDebit()
    {
        // Arrange
        var account = CreateTestAccount(50_000m);
        account.SetOperatingStatus(AccountOperatingStatus.BlockedCredit);

        // Act & Assert
        account.Debit(Money.Bdt(5_000m), "Cheque Cash Withdrawal");
        Assert.Equal(45_000m, account.Balance.Amount);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            account.Credit(Money.Bdt(10_000m), "Credit Attempt"));
        Assert.Equal("AccountCreditBlockedInvariant", ex.InvariantName);
    }

    [Fact]
    public void ReleaseRestraint_PartialHold_RestoresAvailableBalance()
    {
        // Arrange
        var account = CreateTestAccount(100_000m);
        account.ApplyHold(Money.Bdt(30_000m));
        Assert.Equal(70_000m, account.AvailableBalance.Amount);

        // Act
        account.ReleaseHold(Money.Bdt(30_000m));

        // Assert
        Assert.Equal(0m, account.TotalActiveHoldAmount.Amount);
        Assert.Equal(100_000m, account.AvailableBalance.Amount);
    }

    [Fact]
    public void ReleaseRestraint_MakerCheckerSameUser_ThrowsInvariantViolation()
    {
        // Arrange
        var accNo = AccountNumber.Generate("0101", "001", 102);
        var restraint = new AccountRestraint(
            "RST-0002",
            accNo,
            RestraintType.GeneralLien,
            RestraintScope.PartialAmountHold,
            Money.Bdt(25_000m),
            "LoanDefaultLien",
            "Lien against overdue loan installment",
            "Credit Risk Management",
            "CRM-LN-2026-99",
            "MAKER01",
            "CHECKER01",
            DateTime.UtcNow);

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() =>
            restraint.Release("MAKER01", "MAKER01", "Clearance received", DateTime.UtcNow));

        Assert.Equal("MakerCheckerSeparationInvariant", ex.InvariantName);
    }

    [Fact]
    public void RealizeRestraint_DeductsFromBalanceAndReducesHold()
    {
        // Arrange
        var account = CreateTestAccount(100_000m);
        account.ApplyHold(Money.Bdt(30_000m));

        // Act
        var realized = account.RealizeHold(Money.Bdt(30_000m));

        // Assert
        Assert.Equal(30_000m, realized.Amount);
        Assert.Equal(70_000m, account.Balance.Amount);
        Assert.Equal(0m, account.TotalActiveHoldAmount.Amount);
        Assert.Equal(70_000m, account.AvailableBalance.Amount);
    }

    [Fact]
    public void RealizeRestraint_ExceedingHoldAmount_ThrowsInvariantViolation()
    {
        // Arrange
        var accNo = AccountNumber.Generate("0101", "001", 103);
        var restraint = new AccountRestraint(
            "RST-0003",
            accNo,
            RestraintType.NbrGarnisheeOrder,
            RestraintScope.PartialAmountHold,
            Money.Bdt(50_000m),
            "TaxEvasionGarnishee",
            "Tax recovery under NBR Circle 12",
            "National Board of Revenue",
            "NBR-GARN-2026",
            "MAKER01",
            "CHECKER01",
            DateTime.UtcNow);

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() =>
            restraint.ForecloseRealize("CHECKER01", Money.Bdt(60_000m), "GL-TAX-REVENUE", "Court decree", DateTime.UtcNow));

        Assert.Equal("ExceededHoldRealizationInvariant", ex.InvariantName);
    }
}
