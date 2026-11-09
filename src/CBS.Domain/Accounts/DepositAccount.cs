using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Accounts;

// ARCHITECTURAL INTENT: Deposit Account Lifecycle, Inactivity/Dormancy locks,
// NBR statutory tax/excise duty and hovering charges strictly enforced (BRPD Circular 12/2024 Module 3).
public class DepositAccount : AggregateRoot<AccountNumber>
{
    public CifNumber Cif { get; private set; }
    public DepositProductType ProductType { get; private set; }
    public AccountOperatingStatus Status { get; private set; }
    public Money Balance { get; private set; }
    public Money HighestCreditBalanceInYear { get; private set; }
    public Money HoveringCharges { get; private set; }
    public DateOnly OpenedDate { get; private set; }
    public DateOnly LastCustomerTransactionDate { get; private set; }
    public DateOnly? DormantDate { get; private set; }
    public bool HasTin { get; private set; }
    public string BranchCode { get; private set; }
    public string? ReactivatedByUserId { get; private set; }
    public DateTime? ReactivatedAtUtc { get; private set; }

    public DepositAccount(
        AccountNumber accountNumber,
        CifNumber cif,
        DepositProductType productType,
        string branchCode,
        DateOnly openedDate,
        Currency currency,
        bool hasTin = false) : base(accountNumber)
    {
        ArgumentNullException.ThrowIfNull(accountNumber, nameof(accountNumber));
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));
        ArgumentNullException.ThrowIfNull(currency, nameof(currency));
        if (string.IsNullOrWhiteSpace(branchCode)) throw new ArgumentException("Branch code is required.", nameof(branchCode));

        Cif = cif;
        ProductType = productType;
        BranchCode = branchCode.Trim();
        OpenedDate = openedDate;
        LastCustomerTransactionDate = openedDate;
        Status = AccountOperatingStatus.Active;
        Balance = Money.Zero(currency);
        HighestCreditBalanceInYear = Money.Zero(currency);
        HoveringCharges = Money.Zero(currency);
        HasTin = hasTin;
    }

    public void UpdateTinStatus(bool hasTin)
    {
        HasTin = hasTin;
    }

    // 1. INVARIANT: Credit Operations & Hovering Charge Auto-Recovery (BRPD 3.1.19-viii)
    public Money Credit(Money amount, string narration, bool isCustomerInduced = true, DateOnly? transactionDate = null)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        if (amount.Amount <= 0m) throw new ArgumentException("Credit amount must be positive.", nameof(amount));

        if (Status is AccountOperatingStatus.BlockedCredit or AccountOperatingStatus.Frozen or AccountOperatingStatus.Closed or AccountOperatingStatus.Unclaimed10YearTransferred)
        {
            throw new InvariantViolationException(
                "AccountCreditBlockedInvariant",
                "BRPD Circular 12/2024 (3.1.20, 3.1.21)",
                $"Account '{Id.Value}' is in state '{Status}'. Credit transactions prohibited.");
        }

        Balance += amount;

        if (Balance > HighestCreditBalanceInYear)
        {
            HighestCreditBalanceInYear = Balance;
        }

        if (isCustomerInduced)
        {
            LastCustomerTransactionDate = transactionDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        }

        // BRPD 3.1.19-viii: Auto-recovery of hovering charges on future credit availability
        Money recoveredHovering = Money.Zero(Balance.Currency);
        if (HoveringCharges.Amount > 0m)
        {
            if (Balance >= HoveringCharges)
            {
                recoveredHovering = HoveringCharges;
                Balance -= HoveringCharges;
                HoveringCharges = Money.Zero(Balance.Currency);
            }
            else
            {
                recoveredHovering = Balance;
                HoveringCharges -= Balance;
                Balance = Money.Zero(Balance.Currency);
            }
        }

        return recoveredHovering;
    }

    // 2. INVARIANT: Debit Operations & Dormancy Block Lock (BRPD 3.1.20, 1.3.5)
    public void Debit(Money amount, string narration, bool isCustomerInduced = true, DateOnly? transactionDate = null)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        if (amount.Amount <= 0m) throw new ArgumentException("Debit amount must be positive.", nameof(amount));

        // HARD LOCK: Dormancy blocks customer-induced debit transactions (BRPD 3.1.20)
        if (Status == AccountOperatingStatus.Dormant && isCustomerInduced)
        {
            throw new InvariantViolationException(
                "DormantAccountDebitBlockedInvariant",
                "BRPD Circular 12/2024 (3.1.20)",
                $"Account '{Id.Value}' is DORMANT (Inactive since {LastCustomerTransactionDate:yyyy-MM-dd}). Debit transaction blocked until Branch Manager reactivation.");
        }

        if (Status is AccountOperatingStatus.BlockedDebit or AccountOperatingStatus.Frozen or AccountOperatingStatus.Closed or AccountOperatingStatus.Unclaimed10YearTransferred)
        {
            throw new InvariantViolationException(
                "AccountDebitBlockedInvariant",
                "BRPD Circular 12/2024 (3.1.20, 3.1.21)",
                $"Account '{Id.Value}' is in state '{Status}'. Debit transactions prohibited.");
        }

        // HARD LOCK: Insufficient funds invariant (No unauthorized overdraft BRPD 1.3.5)
        if (amount > Balance)
        {
            throw new InvariantViolationException(
                "InsufficientFundsInvariant",
                "BRPD Circular 12/2024 (1.3.5, 3.1.18)",
                $"Account '{Id.Value}' has insufficient balance ({Balance}) for requested debit of {amount}.");
        }

        Balance -= amount;

        if (isCustomerInduced)
        {
            LastCustomerTransactionDate = transactionDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        }
    }

    // 3. INVARIANT: Automated Dormancy State Engine (BRPD 3.1.20)
    // Current Account: 6 months; Savings/SND: 12 months of inactivity
    public bool EvaluateAndApplyDormancy(DateOnly currentDate)
    {
        if (Status != AccountOperatingStatus.Active) return false;

        int monthsInactive = CalculateMonthsDifference(LastCustomerTransactionDate, currentDate);
        int thresholdMonths = ProductType switch
        {
            DepositProductType.CurrentAccount => 6,
            DepositProductType.SavingsAccount => 12,
            DepositProductType.SpecialNoticeDeposit => 12,
            _ => 12
        };

        if (monthsInactive >= thresholdMonths)
        {
            Status = AccountOperatingStatus.Dormant;
            DormantDate = currentDate;
            return true;
        }

        return false;
    }

    // 4. INVARIANT: Branch Manager Reactivation Workflow (BRPD 3.1.20-iv)
    public void ReactivateDormant(string branchManagerUserId, string remarks)
    {
        if (string.IsNullOrWhiteSpace(branchManagerUserId))
            throw new ArgumentException("Branch Manager User ID is required for dormancy reactivation (BRPD 3.1.20-iv).", nameof(branchManagerUserId));
        if (string.IsNullOrWhiteSpace(remarks))
            throw new ArgumentException("Reactivation remarks/KYC review reference is required.", nameof(remarks));

        if (Status != AccountOperatingStatus.Dormant)
        {
            throw new InvalidOperationException($"Cannot reactivate account '{Id.Value}' from status '{Status}'. Account must be 'Dormant'.");
        }

        Status = AccountOperatingStatus.Active;
        DormantDate = null;
        ReactivatedByUserId = branchManagerUserId.Trim();
        ReactivatedAtUtc = DateTime.UtcNow;
    }

    // 5. INVARIANT: 10-Year Inoperative Central Bank Unclaimed Transfer (BRPD 3.1.21-iv)
    public bool Evaluate10YearUnclaimedTransfer(DateOnly currentDate)
    {
        if (Status == AccountOperatingStatus.Closed || Status == AccountOperatingStatus.Unclaimed10YearTransferred)
            return false;

        int monthsInactive = CalculateMonthsDifference(LastCustomerTransactionDate, currentDate);
        if (monthsInactive >= 120) // 10 years = 120 months
        {
            Status = AccountOperatingStatus.Unclaimed10YearTransferred;
            return true;
        }

        return false;
    }

    // 6. INVARIANT: Automated NBR Withholding Tax Assessment (BRPD 3.1.19-i)
    // 10% if customer has valid e-TIN; 15% if no e-TIN
    public Money CalculateWithholdingTax(Money grossInterest)
    {
        decimal rate = HasTin ? 0.10m : 0.15m;
        return new Money(grossInterest.Amount * rate, grossInterest.Currency);
    }

    // 7. INVARIANT: Annual NBR Statutory Excise Duty Slab Assessment (BRPD 3.1.19-vii)
    public Money CalculateAnnualExciseDuty()
    {
        decimal highestAmount = HighestCreditBalanceInYear.Amount;
        decimal dutyAmount = highestAmount switch
        {
            <= 100_000m => 0m,
            <= 500_000m => 150m,
            <= 1_000_000m => 500m,
            <= 10_000_000m => 3_000m,
            <= 50_000_000m => 15_000m,
            _ => 50_000m
        };

        return Money.Bdt(dutyAmount);
    }

    public void AddHoveringCharge(Money chargeAmount)
    {
        ArgumentNullException.ThrowIfNull(chargeAmount);
        HoveringCharges += chargeAmount;
    }

    private static int CalculateMonthsDifference(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate <= fromDate) return 0;
        return ((toDate.Year - fromDate.Year) * 12) + (toDate.Month - fromDate.Month);
    }
}
