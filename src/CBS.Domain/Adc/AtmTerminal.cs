using CBS.Domain.Common;

namespace CBS.Domain.Adc;

/// <summary>
/// Entity representing an ATM or CRM (Cash Recycling Machine) Terminal.
/// Enforces real-time terminal cash reflection under Bangladesh Bank BRPD Circular 12 Clause 7.2.1-vi.
/// </summary>
public sealed class AtmTerminal : Entity<string>
{
    // ARCHITECTURAL INTENT: Real-Time Cash Reflection for ATM / CRM Terminals (BRPD 7.2.1-vi).
    // Terminal cash vault must be updated synchronously upon successful physical dispensation.

    public string TerminalId => Id;
    public string TerminalName { get; private set; } = null!;
    public string BranchCode { get; private set; } = null!;
    public string Location { get; private set; } = null!;
    public decimal CurrentCashBalance { get; private set; }
    public decimal MaxCapacity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime LastReplenishedAtUtc { get; private set; }

    // Parameterless constructor for EF Core ORM materialization
    private AtmTerminal() : base(string.Empty) { }

    public AtmTerminal(
        string terminalId,
        string terminalName,
        string branchCode,
        string location,
        decimal initialCashBalance,
        decimal maxCapacity) : base(terminalId.Trim().ToUpperInvariant())
    {
        if (string.IsNullOrWhiteSpace(terminalId))
            throw new ArgumentException("Terminal ID is required.", nameof(terminalId));
        if (string.IsNullOrWhiteSpace(terminalName))
            throw new ArgumentException("Terminal Name is required.", nameof(terminalName));
        if (string.IsNullOrWhiteSpace(branchCode))
            throw new ArgumentException("Branch Code is required.", nameof(branchCode));
        if (initialCashBalance < 0)
            throw new ArgumentException("Initial cash balance cannot be negative.", nameof(initialCashBalance));
        if (maxCapacity <= 0)
            throw new ArgumentException("Max capacity must be greater than zero.", nameof(maxCapacity));

        TerminalName = terminalName.Trim();
        BranchCode = branchCode.Trim();
        Location = location.Trim();
        CurrentCashBalance = initialCashBalance;
        MaxCapacity = maxCapacity;
        IsActive = true;
        LastReplenishedAtUtc = DateTime.UtcNow;
    }

    public void DispenseCash(decimal amount)
    {
        if (!IsActive)
            throw new InvalidOperationException($"Terminal '{TerminalId}' is inactive or out of service.");
        if (amount <= 0)
            throw new ArgumentException("Dispense amount must be positive.", nameof(amount));
        if (CurrentCashBalance < amount)
            throw new InvalidOperationException($"Security Invariant Violation [BRPD 7.2.1-vi]: ATM terminal '{TerminalId}' has insufficient physical cash. Available: {CurrentCashBalance}, Requested: {amount}.");

        CurrentCashBalance -= amount;
    }

    public void AcceptDeposit(decimal amount)
    {
        if (!IsActive)
            throw new InvalidOperationException($"Terminal '{TerminalId}' is inactive or out of service.");
        if (amount <= 0)
            throw new ArgumentException("Deposit amount must be positive.", nameof(amount));
        if (CurrentCashBalance + amount > MaxCapacity)
            throw new InvalidOperationException($"CRM Terminal cash cassette capacity exceeded. Current: {CurrentCashBalance}, Capacity: {MaxCapacity}.");

        CurrentCashBalance += amount;
    }

    public void ReplenishCash(decimal newCashBalance)
    {
        if (newCashBalance < 0 || newCashBalance > MaxCapacity)
            throw new ArgumentException($"Replenishment cash amount must be between 0 and {MaxCapacity}.", nameof(newCashBalance));

        CurrentCashBalance = newCashBalance;
        LastReplenishedAtUtc = DateTime.UtcNow;
    }

    public void SetActiveStatus(bool active)
    {
        IsActive = active;
    }
}
