using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.BranchCash;

// ARCHITECTURAL INTENT: Teller Drawer / Cash Till Aggregate Root (BRPD Circular 12/2024 Module 1.7.1, 1.7.2).
// Enforces vault-to-teller balancing, strict cashier posting limits, real-time denomination reconciliation,
// and EOD cash discrepancy suspense handling.
public class TellerDrawer : AggregateRoot<Guid>
{
    public string BranchCode { get; private set; }
    public string CashierUserId { get; private set; }
    public DateOnly OperatingDate { get; private set; }
    public Money CashierPostingLimit { get; private set; }
    public Money CashHoldingLimit { get; private set; }
    public Money CurrentBalance { get; private set; }
    public CashDenominationBundle Denominations { get; private set; }
    public TellerDrawerStatus Status { get; private set; }
    public string? OpeningSupervisorUserId { get; private set; }
    public string? ClosingManagerUserId { get; private set; }
    public decimal? DiscrepancyAmount { get; private set; }

    #pragma warning disable CS8618
    protected TellerDrawer() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public TellerDrawer(
        Guid id,
        string branchCode,
        string cashierUserId,
        DateOnly operatingDate,
        Money cashierPostingLimit,
        Money? cashHoldingLimit = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(branchCode)) throw new ArgumentException("Branch code is required.", nameof(branchCode));
        if (string.IsNullOrWhiteSpace(cashierUserId)) throw new ArgumentException("Cashier User ID is required.", nameof(cashierUserId));
        ArgumentNullException.ThrowIfNull(cashierPostingLimit, nameof(cashierPostingLimit));

        BranchCode = branchCode.Trim();
        CashierUserId = cashierUserId.Trim();
        OperatingDate = operatingDate;
        CashierPostingLimit = cashierPostingLimit;
        CashHoldingLimit = cashHoldingLimit ?? Money.Bdt(Math.Max(cashierPostingLimit.Amount * 2m, 1000000m));
        CurrentBalance = Money.Zero(Currency.BDT);
        Denominations = CashDenominationBundle.Empty;
        Status = TellerDrawerStatus.Closed;
    }

    public void Open(
        Money initialVaultAllocation,
        CashDenominationBundle denominations,
        string supervisorUserId)
    {
        if (Status != TellerDrawerStatus.Closed)
        {
            throw new InvariantViolationException(
                "DrawerAlreadyOpenInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii)",
                $"Teller drawer for cashier '{CashierUserId}' is already in status '{Status}'. Cannot open an active or balanced drawer.");
        }

        ArgumentNullException.ThrowIfNull(initialVaultAllocation, nameof(initialVaultAllocation));
        ArgumentNullException.ThrowIfNull(denominations, nameof(denominations));
        if (string.IsNullOrWhiteSpace(supervisorUserId)) throw new ArgumentException("Supervisor User ID is required for vault handoff.", nameof(supervisorUserId));

        // Dual-Control Vault-to-Teller transfer
        if (CashierUserId.Equals(supervisorUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Cashier '{CashierUserId}' cannot act as the vault supervisor for their own cash drawer opening.");
        }

        if (denominations.TotalAmount != initialVaultAllocation)
        {
            throw new InvariantViolationException(
                "VaultCashDenominationMismatchInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii, 1.7.2)",
                $"Vault cash allocation amount ({initialVaultAllocation}) does not match denomination bundle sum ({denominations.TotalAmount}).");
        }

        OpeningSupervisorUserId = supervisorUserId.Trim();
        CurrentBalance = initialVaultAllocation;
        Denominations = denominations;
        Status = TellerDrawerStatus.Active;
    }

    public void ProcessCashDeposit(
        Money amount,
        CashDenominationBundle denominations,
        string? supervisorApprovalId = null)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(denominations, nameof(denominations));

        if (denominations.TotalAmount != amount)
        {
            throw new InvariantViolationException(
                "CashDenominationMismatchInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii)",
                $"Deposit transaction amount ({amount}) does not match denomination bundle sum ({denominations.TotalAmount}).");
        }

        // Cashier posting limit check (BRPD 1.7.1-ii)
        if (amount > CashierPostingLimit && string.IsNullOrWhiteSpace(supervisorApprovalId))
        {
            throw new InvariantViolationException(
                "CashierPostingLimitExceededInvariant",
                "BRPD Circular 12/2024 (1.7.1-ii)",
                $"Cash deposit amount ({amount}) exceeds cashier posting limit ({CashierPostingLimit}). Dual supervisor authorization is mandatory.");
        }

        // Cashier drawer holding limit check (BRPD 1.7.1-ii)
        if (CurrentBalance + amount > CashHoldingLimit && string.IsNullOrWhiteSpace(supervisorApprovalId))
        {
            throw new InvariantViolationException(
                "TellerCashHoldingLimitExceededInvariant",
                "BRPD Circular 12/2024 (1.7.1-ii)",
                $"Cash deposit would cause drawer balance ({CurrentBalance + amount}) to exceed cashier holding limit ({CashHoldingLimit}). Escalation to supervisor approval queue is mandatory.");
        }

        CurrentBalance += amount;
        Denominations = Denominations.Add(denominations);
    }

    public void SwapDenominations(
        CashDenominationBundle givenBundle,
        CashDenominationBundle receivedBundle,
        string supervisorApprovalId)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(givenBundle, nameof(givenBundle));
        ArgumentNullException.ThrowIfNull(receivedBundle, nameof(receivedBundle));

        if (string.IsNullOrWhiteSpace(supervisorApprovalId))
        {
            throw new InvariantViolationException(
                "SupervisorAuthorizationRequiredInvariant",
                "BRPD Circular 12/2024 (1.7.1-ii, 1.7.2-ii)",
                "Inter-teller denomination swap requires verified supervisor authorization.");
        }

        if (givenBundle.TotalAmount != receivedBundle.TotalAmount)
        {
            throw new InvariantViolationException(
                "DenominationSwapValueMismatchInvariant",
                "BRPD Circular 12/2024 (1.7.2-ii)",
                $"Given bundle sum ({givenBundle.TotalAmount}) must exactly equal received bundle sum ({receivedBundle.TotalAmount}). Net drawer cash change must be zero.");
        }

        Denominations = Denominations.Subtract(givenBundle);
        Denominations = Denominations.Add(receivedBundle);
    }

    public void ProcessCashWithdrawal(
        Money amount,
        CashDenominationBundle denominations,
        string? supervisorApprovalId = null)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(denominations, nameof(denominations));

        if (denominations.TotalAmount != amount)
        {
            throw new InvariantViolationException(
                "CashDenominationMismatchInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii)",
                $"Withdrawal transaction amount ({amount}) does not match denomination bundle sum ({denominations.TotalAmount}).");
        }

        if (amount > CurrentBalance)
        {
            throw new InvariantViolationException(
                "InsufficientDrawerCashInvariant",
                "BRPD Circular 12/2024 (1.7.1)",
                $"Teller drawer has insufficient cash ({CurrentBalance}) to dispense withdrawal amount ({amount}).");
        }

        // Cashier posting limit check (BRPD 1.7.1-ii)
        if (amount > CashierPostingLimit && string.IsNullOrWhiteSpace(supervisorApprovalId))
        {
            throw new InvariantViolationException(
                "CashierPostingLimitExceededInvariant",
                "BRPD Circular 12/2024 (1.7.1-ii)",
                $"Cash withdrawal amount ({amount}) exceeds cashier posting limit ({CashierPostingLimit}). Dual supervisor authorization is mandatory.");
        }

        CurrentBalance -= amount;
        Denominations = Denominations.Subtract(denominations);
    }

    public void TransferToVault(
        Money amount,
        CashDenominationBundle denominations,
        string vaultCustodianUserId)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(denominations, nameof(denominations));
        if (string.IsNullOrWhiteSpace(vaultCustodianUserId)) throw new ArgumentException("Vault custodian User ID is required.", nameof(vaultCustodianUserId));

        if (denominations.TotalAmount != amount)
        {
            throw new InvariantViolationException(
                "CashDenominationMismatchInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii)",
                $"Vault transfer amount ({amount}) does not match denomination bundle sum ({denominations.TotalAmount}).");
        }

        if (amount > CurrentBalance)
        {
            throw new InvariantViolationException(
                "InsufficientDrawerCashInvariant",
                "BRPD Circular 12/2024 (1.7.1)",
                $"Teller drawer balance ({CurrentBalance}) is insufficient for vault remittance of {amount}.");
        }

        CurrentBalance -= amount;
        Denominations = Denominations.Subtract(denominations);
    }

    public void CloseAndBalanceDrawer(
        CashDenominationBundle physicalCount,
        string branchManagerUserId,
        out decimal discrepancy)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(physicalCount, nameof(physicalCount));
        if (string.IsNullOrWhiteSpace(branchManagerUserId)) throw new ArgumentException("Branch Manager User ID is required for EOD cash balancing.", nameof(branchManagerUserId));

        ClosingManagerUserId = branchManagerUserId.Trim();
        discrepancy = physicalCount.TotalAmount.Amount - CurrentBalance.Amount;
        DiscrepancyAmount = discrepancy;

        if (discrepancy == 0m)
        {
            Status = TellerDrawerStatus.Balanced;
        }
        else
        {
            // Discrepancy exists: Cash shortage or excess must be parked in suspense account (BRPD 1.7.2-ii)
            Status = TellerDrawerStatus.DiscrepancySuspense;
        }
    }

    private void EnsureActive()
    {
        if (Status != TellerDrawerStatus.Active)
        {
            throw new InvariantViolationException(
                "DrawerNotActiveInvariant",
                "BRPD Circular 12/2024 (1.7.1)",
                $"Operation prohibited. Teller drawer for cashier '{CashierUserId}' is not active (Current status: {Status}).");
        }
    }
}
