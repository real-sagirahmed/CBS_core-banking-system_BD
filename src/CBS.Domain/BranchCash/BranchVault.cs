using CBS.Domain.Common;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.BranchCash;

// ARCHITECTURAL INTENT: Branch Vault Dual-Custody & Insurance Cap (BRPD Circular 12/2024 Module 1.7.1-iii, 1.7.12).
// Controls physical vault cash balances, dual-key holder custody (Head Cashier & Branch Manager),
// central bank currency chest movements, teller replenishment, and overnight insurance limit enforcement.
public class BranchVault : AggregateRoot<Guid>
{
    public string BranchCode { get; private set; }
    public string VaultCustodianUserId { get; private set; }
    public string JointCustodianUserId { get; private set; }
    public Money CurrentBalance { get; private set; }
    public Money InsuredCashLimit { get; private set; }
    public CashDenominationBundle Denominations { get; private set; }
    public bool IsVaultLocked { get; private set; }
    public bool EveningBankingSessionActive { get; private set; }
    public DateTime? EveningSessionOpenedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastModifiedAtUtc { get; private set; }

#pragma warning disable CS8618
    protected BranchVault() : base(Guid.Empty) { }
#pragma warning restore CS8618

    public BranchVault(
        Guid id,
        string branchCode,
        string vaultCustodianUserId,
        string jointCustodianUserId,
        Money initialBalance,
        Money insuredCashLimit,
        CashDenominationBundle denominations) : base(id)
    {
        if (string.IsNullOrWhiteSpace(branchCode))
            throw new ArgumentException("Branch code is required.", nameof(branchCode));
        if (string.IsNullOrWhiteSpace(vaultCustodianUserId))
            throw new ArgumentException("Vault custodian user ID is required.", nameof(vaultCustodianUserId));
        if (string.IsNullOrWhiteSpace(jointCustodianUserId))
            throw new ArgumentException("Joint custodian user ID is required.", nameof(jointCustodianUserId));
        ArgumentNullException.ThrowIfNull(initialBalance, nameof(initialBalance));
        ArgumentNullException.ThrowIfNull(insuredCashLimit, nameof(insuredCashLimit));
        ArgumentNullException.ThrowIfNull(denominations, nameof(denominations));

        // Dual custody key separation invariant
        if (vaultCustodianUserId.Trim().Equals(jointCustodianUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "DualCustodySeparationInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii-d)",
                "Vault Custodian and Joint Custodian must be distinct individuals to maintain dual-key control.");
        }

        if (denominations.TotalAmount != initialBalance)
        {
            throw new InvariantViolationException(
                "VaultCashDenominationMismatchInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii, 1.7.2)",
                $"Initial vault cash balance ({initialBalance}) does not match denomination bundle sum ({denominations.TotalAmount}).");
        }

        if (insuredCashLimit.Amount <= 0m)
        {
            throw new InvariantViolationException(
                "PositiveInsuredLimitInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii-a)",
                "Branch insured cash limit must be strictly positive.");
        }

        BranchCode = branchCode.Trim();
        VaultCustodianUserId = vaultCustodianUserId.Trim();
        JointCustodianUserId = jointCustodianUserId.Trim();
        CurrentBalance = initialBalance;
        InsuredCashLimit = insuredCashLimit;
        Denominations = denominations;
        IsVaultLocked = false;
        EveningBankingSessionActive = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void DepositCashFromExternalChest(
        Money amount,
        CashDenominationBundle denominations,
        string chestReference,
        string custodianUserId,
        string jointCustodianUserId)
    {
        EnsureVaultAccessible();
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(denominations, nameof(denominations));
        if (string.IsNullOrWhiteSpace(chestReference))
            throw new ArgumentException("Chest reference is required.", nameof(chestReference));

        VerifyDualCustody(custodianUserId, jointCustodianUserId);

        if (denominations.TotalAmount != amount)
        {
            throw new InvariantViolationException(
                "ChestCashDenominationMismatchInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii, 1.7.2)",
                $"External chest deposit amount ({amount}) does not match denomination bundle sum ({denominations.TotalAmount}).");
        }

        CurrentBalance += amount;
        Denominations = Denominations.Add(denominations);
        LastModifiedAtUtc = DateTime.UtcNow;
    }

    public void RemitCashToExternalChest(
        Money amount,
        CashDenominationBundle denominations,
        string chestReference,
        string custodianUserId,
        string jointCustodianUserId)
    {
        EnsureVaultAccessible();
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(denominations, nameof(denominations));
        if (string.IsNullOrWhiteSpace(chestReference))
            throw new ArgumentException("Chest reference is required.", nameof(chestReference));

        VerifyDualCustody(custodianUserId, jointCustodianUserId);

        if (denominations.TotalAmount != amount)
        {
            throw new InvariantViolationException(
                "ChestCashDenominationMismatchInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii, 1.7.2)",
                $"External chest remittance amount ({amount}) does not match denomination bundle sum ({denominations.TotalAmount}).");
        }

        if (amount > CurrentBalance)
        {
            throw new InvariantViolationException(
                "InsufficientVaultCashInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii)",
                $"Branch vault balance ({CurrentBalance}) is insufficient for chest remittance of {amount}.");
        }

        CurrentBalance -= amount;
        Denominations = Denominations.Subtract(denominations);
        LastModifiedAtUtc = DateTime.UtcNow;
    }

    public void AllocateCashToTeller(
        Money amount,
        CashDenominationBundle denominations,
        string cashierUserId,
        string custodianUserId)
    {
        EnsureVaultAccessible();
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(denominations, nameof(denominations));
        if (string.IsNullOrWhiteSpace(cashierUserId))
            throw new ArgumentException("Cashier User ID is required.", nameof(cashierUserId));
        if (string.IsNullOrWhiteSpace(custodianUserId))
            throw new ArgumentException("Custodian User ID is required.", nameof(custodianUserId));

        if (cashierUserId.Trim().Equals(custodianUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Cashier '{cashierUserId}' cannot act as the vault custodian releasing funds to their own till.");
        }

        if (denominations.TotalAmount != amount)
        {
            throw new InvariantViolationException(
                "VaultCashDenominationMismatchInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii, 1.7.2)",
                $"Teller replenishment amount ({amount}) does not match denomination bundle sum ({denominations.TotalAmount}).");
        }

        if (amount > CurrentBalance)
        {
            throw new InvariantViolationException(
                "InsufficientVaultCashInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii)",
                $"Branch vault balance ({CurrentBalance}) is insufficient for teller allocation of {amount}.");
        }

        CurrentBalance -= amount;
        Denominations = Denominations.Subtract(denominations);
        LastModifiedAtUtc = DateTime.UtcNow;
    }

    public void ReceiveCashFromTeller(
        Money amount,
        CashDenominationBundle denominations,
        string cashierUserId,
        string custodianUserId)
    {
        EnsureVaultAccessible();
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(denominations, nameof(denominations));
        if (string.IsNullOrWhiteSpace(cashierUserId))
            throw new ArgumentException("Cashier User ID is required.", nameof(cashierUserId));
        if (string.IsNullOrWhiteSpace(custodianUserId))
            throw new ArgumentException("Custodian User ID is required.", nameof(custodianUserId));

        if (cashierUserId.Trim().Equals(custodianUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Cashier '{cashierUserId}' cannot act as the vault custodian receiving funds from their own till.");
        }

        if (denominations.TotalAmount != amount)
        {
            throw new InvariantViolationException(
                "VaultCashDenominationMismatchInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii, 1.7.2)",
                $"Teller deposit to vault amount ({amount}) does not match denomination bundle sum ({denominations.TotalAmount}).");
        }

        CurrentBalance += amount;
        Denominations = Denominations.Add(denominations);
        LastModifiedAtUtc = DateTime.UtcNow;
    }

    public bool IsInsuredLimitExceeded() => CurrentBalance > InsuredCashLimit;

    public decimal GetExcessOverInsuredLimit() => Math.Max(0m, CurrentBalance.Amount - InsuredCashLimit.Amount);

    public void OpenEveningBankingSession(string authorizedOfficerUserId)
    {
        EnsureVaultAccessible();
        if (string.IsNullOrWhiteSpace(authorizedOfficerUserId))
            throw new ArgumentException("Authorized officer user ID is required.", nameof(authorizedOfficerUserId));

        EveningBankingSessionActive = true;
        EveningSessionOpenedAtUtc = DateTime.UtcNow;
        LastModifiedAtUtc = DateTime.UtcNow;
    }

    public void CloseEveningBankingSession(string authorizedOfficerUserId)
    {
        if (string.IsNullOrWhiteSpace(authorizedOfficerUserId))
            throw new ArgumentException("Authorized officer user ID is required.", nameof(authorizedOfficerUserId));

        EveningBankingSessionActive = false;
        EveningSessionOpenedAtUtc = null;
        LastModifiedAtUtc = DateTime.UtcNow;
    }

    public void LockVault(string custodianUserId, string jointCustodianUserId)
    {
        VerifyDualCustody(custodianUserId, jointCustodianUserId);
        IsVaultLocked = true;
        LastModifiedAtUtc = DateTime.UtcNow;
    }

    public void UnlockVault(string custodianUserId, string jointCustodianUserId)
    {
        VerifyDualCustody(custodianUserId, jointCustodianUserId);
        IsVaultLocked = false;
        LastModifiedAtUtc = DateTime.UtcNow;
    }

    private void EnsureVaultAccessible()
    {
        if (IsVaultLocked)
        {
            throw new InvariantViolationException(
                "VaultLockedInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii)",
                $"Branch vault for branch '{BranchCode}' is locked under dual-custody. Unlock vault prior to cash operations.");
        }
    }

    private void VerifyDualCustody(string custodianUserId, string jointCustodianUserId)
    {
        if (string.IsNullOrWhiteSpace(custodianUserId) || string.IsNullOrWhiteSpace(jointCustodianUserId))
        {
            throw new InvariantViolationException(
                "MissingDualCustodyInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii-d)",
                "Vault operations require simultaneous dual-custody authorization from both key holders.");
        }

        if (custodianUserId.Trim().Equals(jointCustodianUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "DualCustodySeparationInvariant",
                "BRPD Circular 12/2024 (1.7.1-iii-d)",
                "Vault dual-custody requires two distinct officers. A single officer cannot provide both authorizations.");
        }
    }
}
