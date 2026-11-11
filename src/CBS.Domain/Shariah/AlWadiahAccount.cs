using CBS.Domain.Common;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Shariah;

// ARCHITECTURAL INTENT: Al-Wadiah operates under the Islamic contract of safe-custody (Amanah).
// The bank guarantees 100% of customer principal with strict 0.00% interest, profit, or dividend promise (BRPD 3.3.1, 3.3.3).
public class AlWadiahAccount : AggregateRoot<Guid>
{
    public string AccountNumber { get; private set; }
    public string Cif { get; private set; }
    public string Currency { get; private set; }
    public decimal Balance { get; private set; }
    public decimal InterestRate { get; private set; } = 0.00m; // Strict 0% invariant
    public string SafeCustodyContractRef { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    protected AlWadiahAccount() : base(Guid.NewGuid()) { }
    #pragma warning restore CS8618

    public AlWadiahAccount(
        string accountNumber,
        string cif,
        string currency,
        string safeCustodyContractRef) : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(accountNumber)) throw new ArgumentException("Account number required.", nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(cif)) throw new ArgumentException("CIF required.", nameof(cif));
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency required.", nameof(currency));
        if (string.IsNullOrWhiteSpace(safeCustodyContractRef)) throw new ArgumentException("Safe custody contract ref required.", nameof(safeCustodyContractRef));

        AccountNumber = accountNumber.Trim();
        Cif = cif.Trim();
        Currency = currency.Trim().ToUpperInvariant();
        Balance = 0m;
        InterestRate = 0.00m;
        SafeCustodyContractRef = safeCustodyContractRef.Trim();
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Deposit(decimal amount)
    {
        if (!IsActive) throw new InvariantViolationException("InactiveAccountInvariant", "BRPD 3.3.1", "Cannot deposit to inactive Al-Wadiah account.");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Deposit amount must be positive.");
        Balance += amount;
    }

    public void Withdraw(decimal amount)
    {
        if (!IsActive) throw new InvariantViolationException("InactiveAccountInvariant", "BRPD 3.3.1", "Cannot withdraw from inactive Al-Wadiah account.");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Withdrawal amount must be positive.");
        if (amount > Balance)
        {
            throw new InvariantViolationException(
                "InsufficientBalanceInvariant",
                "BRPD 3.3.3",
                $"Insufficient funds. Current balance: {Balance:F2}, requested withdrawal: {amount:F2}.");
        }
        Balance -= amount;
    }

    public void CloseAccount()
    {
        if (Balance > 0)
        {
            throw new InvariantViolationException(
                "AccountBalanceNotZeroInvariant",
                "BRPD 3.3.1",
                $"Cannot close Al-Wadiah account with remaining balance {Balance:F2}. Balance must be zeroed first.");
        }
        IsActive = false;
    }
}
