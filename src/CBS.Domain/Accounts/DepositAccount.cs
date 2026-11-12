using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Accounts;

// ARCHITECTURAL INTENT: Deposit Account Lifecycle, Inactivity/Dormancy/Inoperative locks,
// 10-Year Unclaimed Fund Transfer (BRPD 3.1.21-iv), NBR statutory tax/excise duty,
// Leap-Year Actual/365/366 daily interest accrual & capitalization,
// Account Restraints/Liens/Court Freezes (BRPD 1.6.2-v, 1.6.6, 3.1.15),
// and Statutory Account Closure Invariant Locks (BRPD 1.6.4, 3.1.19).
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

    // Interest & Provisioning state (BRPD 1.2.2-i, 1.3.7, 3.2.2)
    public decimal AnnualInterestRate { get; private set; }
    public Money AccruedInterestProvision { get; private set; }
    public DateOnly? LastInterestCalculationDate { get; private set; }
    public DateOnly? LastInterestCapitalizationDate { get; private set; }

    // 10-Year Unclaimed Fund Transfer & Notice Tracking (BRPD 3.1.21-iv)
    public int UnclaimedNoticesServedCount { get; private set; }
    public DateOnly? LastUnclaimedNoticeDate { get; private set; }

    // Maker-Checker Reactivation Separation (BRPD 1.3.18, 3.1.20-iv)
    public string? ReactivationRequestedByUserId { get; private set; }
    public string? ReactivationKycDocumentReference { get; private set; }
    public DateTime? ReactivationRequestedAtUtc { get; private set; }
    public bool IsReactivationPending { get; private set; }

    // Account Restraints, Lien Holds & Available Balance (BRPD 1.6.2-v, 1.6.6, 3.1.15-ix, x)
    public Money TotalActiveHoldAmount { get; private set; }
    public Money AvailableBalance => Money.Bdt(Math.Max(0m, Balance.Amount - TotalActiveHoldAmount.Amount));

    // Statutory Account Closure Audit (BRPD 1.6.4, 3.1.19-iv, v)
    public string? ClosedByUserId { get; private set; }
    public string? ClosedAuthorizedByUserId { get; private set; }
    public DateOnly? ClosedDate { get; private set; }
    public string? ClosingReason { get; private set; }

    #pragma warning disable CS8618
    protected DepositAccount() : base(null!) { }
    #pragma warning restore CS8618

    public DepositAccount(
        AccountNumber accountNumber,
        CifNumber cif,
        DepositProductType productType,
        string branchCode,
        DateOnly openedDate,
        Currency currency,
        bool hasTin = false,
        decimal? annualInterestRate = null) : base(accountNumber)
    {
        ArgumentNullException.ThrowIfNull(accountNumber, nameof(accountNumber));
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));
        ArgumentNullException.ThrowIfNull(currency, nameof(currency));
        if (string.IsNullOrWhiteSpace(branchCode)) throw new ArgumentException("Branch code is required.", nameof(branchCode));

        // BRPD 3.1.1: Current accounts are strictly non-interest bearing (0.00%)
        if (productType == DepositProductType.CurrentAccount && annualInterestRate.HasValue && annualInterestRate.Value > 0m)
        {
            throw new InvariantViolationException(
                "CurrentAccountInterestProhibitedInvariant",
                "BRPD Circular 12/2024 (3.1.1, 3.2.4)",
                "Demand Current accounts cannot bear positive interest rate.");
        }

        decimal defaultRate = productType switch
        {
            DepositProductType.CurrentAccount => 0.00m,
            DepositProductType.SavingsAccount => 4.00m,
            DepositProductType.SpecialNoticeDeposit => 2.50m,
            DepositProductType.FixedDepositReceipt => 7.00m,
            DepositProductType.MonthlySavingsScheme => 7.50m,
            _ => 0.00m
        };

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
        AnnualInterestRate = annualInterestRate ?? defaultRate;
        AccruedInterestProvision = Money.Zero(currency);
        UnclaimedNoticesServedCount = 0;
        IsReactivationPending = false;
        TotalActiveHoldAmount = Money.Zero(currency);
    }

    public void UpdateTinStatus(bool hasTin)
    {
        HasTin = hasTin;
    }

    public void UpdateInterestRate(decimal newRate)
    {
        if (ProductType == DepositProductType.CurrentAccount && newRate > 0m)
        {
            throw new InvariantViolationException(
                "CurrentAccountInterestProhibitedInvariant",
                "BRPD Circular 12/2024 (3.1.1, 3.2.4)",
                "Demand Current accounts cannot bear positive interest rate.");
        }

        if (newRate < 0m) throw new ArgumentException("Interest rate cannot be negative.", nameof(newRate));
        AnnualInterestRate = newRate;
    }

    public void SetOperatingStatus(AccountOperatingStatus newStatus)
    {
        Status = newStatus;
    }

    // 1. INVARIANT: Credit Operations & Hovering Charge Auto-Recovery (BRPD 3.1.19-viii, 1.6.2-v, 3.1.15-vii)
    public Money Credit(Money amount, string narration, bool isCustomerInduced = true, DateOnly? transactionDate = null)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        if (amount.Amount <= 0m) throw new ArgumentException("Credit amount must be positive.", nameof(amount));

        if (Status is AccountOperatingStatus.BlockedCredit or AccountOperatingStatus.Frozen or AccountOperatingStatus.Closed or AccountOperatingStatus.Unclaimed10YearTransferred)
        {
            throw new InvariantViolationException(
                "AccountCreditBlockedInvariant",
                "BRPD Circular 12/2024 (1.6.2-v, 3.1.15-vii, 3.1.20, 3.1.21)",
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

    // 2. INVARIANT: Debit Operations, Lien Holds & Dormancy/Inoperative Block Lock (BRPD 1.6.2-v, 1.6.6, 3.1.15, 3.1.20, 3.1.21, 1.3.5)
    public void Debit(Money amount, string narration, bool isCustomerInduced = true, DateOnly? transactionDate = null)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        if (amount.Amount <= 0m) throw new ArgumentException("Debit amount must be positive.", nameof(amount));

        // HARD LOCK: Dormant and Inoperative accounts block customer-induced debit transactions (BRPD 3.1.20, 3.1.21)
        if ((Status == AccountOperatingStatus.Dormant || Status == AccountOperatingStatus.Inoperative) && isCustomerInduced)
        {
            throw new InvariantViolationException(
                "DormantAccountDebitBlockedInvariant",
                "BRPD Circular 12/2024 (3.1.20, 3.1.21)",
                $"Account '{Id.Value}' is {Status} (Inactive since {LastCustomerTransactionDate:yyyy-MM-dd}). Debit transaction blocked until Branch Manager reactivation.");
        }

        // HARD LOCK: BlockedDebit, Frozen, Closed, and Unclaimed accounts prohibit debit transactions (BRPD 1.6.2-v, 3.1.15-vi)
        if (Status is AccountOperatingStatus.BlockedDebit or AccountOperatingStatus.Frozen or AccountOperatingStatus.Closed or AccountOperatingStatus.Unclaimed10YearTransferred)
        {
            throw new InvariantViolationException(
                "AccountDebitBlockedInvariant",
                "BRPD Circular 12/2024 (1.6.2-v, 3.1.15-vi, 3.1.20, 3.1.21)",
                $"Account '{Id.Value}' is in state '{Status}'. Debit transactions prohibited.");
        }

        // HARD LOCK: Available balance invariant considering active lien/court holds (BRPD 1.6.6, 3.1.15-ix, x)
        if (TotalActiveHoldAmount.Amount > 0m && amount > AvailableBalance)
        {
            throw new InvariantViolationException(
                "InsufficientAvailableBalanceAfterLienInvariant",
                "BRPD Circular 12/2024 (1.6.6, 3.1.15-ix, x)",
                $"Account '{Id.Value}' has ledger balance {Balance} but active holds of {TotalActiveHoldAmount}. Available balance {AvailableBalance} is insufficient for requested debit of {amount}.");
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

    // 2.1 HOLD ENGINE: Apply, Release & Realize Holds (BRPD 1.6.6, 3.1.15-ix, x)
    public void ApplyHold(Money holdAmount)
    {
        ArgumentNullException.ThrowIfNull(holdAmount, nameof(holdAmount));
        if (holdAmount.Amount <= 0m) throw new ArgumentException("Hold amount must be positive.", nameof(holdAmount));

        // BRPD 3.1.15-x: "System shall have ability to block any amount irrespective of the available balance of the account."
        TotalActiveHoldAmount += holdAmount;
    }

    public void ReleaseHold(Money releaseAmount)
    {
        ArgumentNullException.ThrowIfNull(releaseAmount, nameof(releaseAmount));
        if (releaseAmount.Amount <= 0m) throw new ArgumentException("Release amount must be positive.", nameof(releaseAmount));

        decimal newAmount = Math.Max(0m, TotalActiveHoldAmount.Amount - releaseAmount.Amount);
        TotalActiveHoldAmount = Money.Bdt(newAmount);
    }

    public Money RealizeHold(Money realizeAmount)
    {
        ArgumentNullException.ThrowIfNull(realizeAmount, nameof(realizeAmount));
        if (realizeAmount.Amount <= 0m) throw new ArgumentException("Realize amount must be positive.", nameof(realizeAmount));

        // Realize debit from balance up to available balance or total balance
        Money actualDebited = realizeAmount > Balance ? Balance : realizeAmount;
        Balance -= actualDebited;

        decimal newHoldAmount = Math.Max(0m, TotalActiveHoldAmount.Amount - realizeAmount.Amount);
        TotalActiveHoldAmount = Money.Bdt(newHoldAmount);

        return actualDebited;
    }

    // 2.2 CLOSING LIFECYCLE: Statutory Account Closure Invariant Locks (BRPD 1.6.4, 3.1.19-iv, v)
    public void CloseAccount(
        string makerUserId,
        string checkerUserId,
        string reason,
        DateOnly closeDate,
        out Money closingFee,
        out Money vat,
        out Money netRefund)
    {
        if (string.IsNullOrWhiteSpace(makerUserId)) throw new ArgumentException("Maker User ID is required.", nameof(makerUserId));
        if (string.IsNullOrWhiteSpace(checkerUserId)) throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Closure reason is required.", nameof(reason));

        if (Status == AccountOperatingStatus.Closed)
        {
            throw new InvalidOperationException($"Account '{Id.Value}' is already closed.");
        }

        // HARD LOCK: Dual-Control Maker-Checker segregation (BRPD 1.3.18, 10.3.5-x)
        if (makerUserId.Trim().Equals(checkerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Maker '{makerUserId}' cannot authorize account closure. Strict dual-control duty segregation required.");
        }

        // HARD LOCK: Active restraints & lien holds block closure (BRPD 1.6.2-v, 3.1.15-i)
        if (TotalActiveHoldAmount.Amount > 0m)
        {
            throw new InvariantViolationException(
                "ActiveRestraintsHoldInvariant",
                "BRPD Circular 12/2024 (1.6.2-v, 3.1.15-i)",
                $"Account '{Id.Value}' has active restraint holds of {TotalActiveHoldAmount}. Cannot close account under active lien or legal restraint.");
        }

        // HARD LOCK: Unapplied accrued interest in provision blocks closure (BRPD 1.6.4-iv)
        if (AccruedInterestProvision.Amount > 0m)
        {
            throw new InvariantViolationException(
                "UnappliedInterestProvisionInvariant",
                "BRPD Circular 12/2024 (1.6.4-iv)",
                $"Account '{Id.Value}' has unapplied accrued interest of {AccruedInterestProvision}. Accrued interest must be capitalized or adjusted before closure.");
        }

        // Statutory closing charge: BDT 300 fee + 15% NBR VAT (BDT 45) = BDT 345 (BRPD 3.1.19-iv, v)
        decimal feeAmount = 300.00m;
        decimal vatAmount = Math.Round(feeAmount * 0.15m, 2, MidpointRounding.AwayFromZero);
        decimal totalDeduction = feeAmount + vatAmount; // 345.00 BDT

        if (Balance.Amount >= totalDeduction)
        {
            closingFee = Money.Bdt(feeAmount);
            vat = Money.Bdt(vatAmount);
            netRefund = Balance - Money.Bdt(totalDeduction);
        }
        else
        {
            // If balance is less than statutory closing charge, deduct entire balance towards fee
            closingFee = Balance;
            vat = Money.Zero(Balance.Currency);
            netRefund = Money.Zero(Balance.Currency);
        }

        Balance = Money.Zero(Balance.Currency);
        Status = AccountOperatingStatus.Closed;
        ClosedByUserId = makerUserId.Trim();
        ClosedAuthorizedByUserId = checkerUserId.Trim();
        ClosedDate = closeDate;
        ClosingReason = reason.Trim();
    }

    // 3. INVARIANT: Automated Dormancy and Inoperative State Engine (BRPD 3.1.20, 3.1.21)
    public bool EvaluateAndApplyDormancy(DateOnly currentDate)
    {
        if (Status is AccountOperatingStatus.Closed or AccountOperatingStatus.Unclaimed10YearTransferred or AccountOperatingStatus.Frozen)
            return false;

        int monthsInactive = CalculateMonthsDifference(LastCustomerTransactionDate, currentDate);

        // Inoperative check (BRPD 3.1.21): >= 24 months of customer inactivity
        if (monthsInactive >= 24)
        {
            if (Status != AccountOperatingStatus.Inoperative)
            {
                Status = AccountOperatingStatus.Inoperative;
                DormantDate ??= currentDate;
                return true;
            }
            return false;
        }

        // Dormant check (BRPD 3.1.20): 6 months for Current Account, 12 months for Savings/SND
        int thresholdMonths = ProductType switch
        {
            DepositProductType.CurrentAccount => 6,
            DepositProductType.SavingsAccount => 12,
            DepositProductType.SpecialNoticeDeposit => 12,
            _ => 12
        };

        if (monthsInactive >= thresholdMonths)
        {
            if (Status != AccountOperatingStatus.Dormant)
            {
                Status = AccountOperatingStatus.Dormant;
                DormantDate = currentDate;
                return true;
            }
            return false;
        }

        return false;
    }

    // 4. INVARIANT: Unclaimed Notice Dispatch Engine (BRPD 3.1.21-iv)
    // Serves 3 notices: Notice 1 @ 9 yrs (108m), Notice 2 @ 9.5 yrs (114m), Final Notice @ 9.9 yrs (119m)
    public int EvaluateUnclaimedNotices(DateOnly currentDate)
    {
        if (Status is AccountOperatingStatus.Closed or AccountOperatingStatus.Unclaimed10YearTransferred)
            return 0;

        int monthsInactive = CalculateMonthsDifference(LastCustomerTransactionDate, currentDate);

        if (monthsInactive >= 119 && UnclaimedNoticesServedCount < 3)
        {
            UnclaimedNoticesServedCount = 3;
            LastUnclaimedNoticeDate = currentDate;
            return 3;
        }

        if (monthsInactive >= 114 && UnclaimedNoticesServedCount < 2)
        {
            UnclaimedNoticesServedCount = 2;
            LastUnclaimedNoticeDate = currentDate;
            return 2;
        }

        if (monthsInactive >= 108 && UnclaimedNoticesServedCount < 1)
        {
            UnclaimedNoticesServedCount = 1;
            LastUnclaimedNoticeDate = currentDate;
            return 1;
        }

        return 0;
    }

    // 5. INVARIANT: 10-Year Inoperative Central Bank Unclaimed Transfer Execution (BRPD 3.1.21-iv)
    public Money Execute10YearUnclaimedTransfer(DateOnly currentDate)
    {
        if (Status is AccountOperatingStatus.Closed or AccountOperatingStatus.Unclaimed10YearTransferred)
        {
            throw new InvalidOperationException($"Account '{Id.Value}' is already in state '{Status}'. Cannot re-execute unclaimed transfer.");
        }

        int monthsInactive = CalculateMonthsDifference(LastCustomerTransactionDate, currentDate);
        if (monthsInactive < 120) // 10 years = 120 months
        {
            throw new InvariantViolationException(
                "PrematureUnclaimedTransferInvariant",
                "BRPD Circular 12/2024 (3.1.21-iv)",
                $"Account '{Id.Value}' has been inactive for {monthsInactive} months. Minimum 120 months (10 years) required for statutory transfer to Bangladesh Bank.");
        }

        var transferredAmount = Balance;
        Balance = Money.Zero(transferredAmount.Currency);
        Status = AccountOperatingStatus.Unclaimed10YearTransferred;
        DormantDate = currentDate;

        return transferredAmount;
    }

    // Backwards-compatible direct check
    public bool Evaluate10YearUnclaimedTransfer(DateOnly currentDate)
    {
        if (Status is AccountOperatingStatus.Closed or AccountOperatingStatus.Unclaimed10YearTransferred)
            return false;

        int monthsInactive = CalculateMonthsDifference(LastCustomerTransactionDate, currentDate);
        if (monthsInactive >= 120)
        {
            Execute10YearUnclaimedTransfer(currentDate);
            return true;
        }

        return false;
    }

    // 6. INVARIANT: Maker-Checker Dormancy Reactivation Workflow (BRPD 1.3.18, 3.1.20-iv, 10.3.5-x)
    public void RequestDormancyReactivation(string makerUserId, string kycDocumentReference)
    {
        if (string.IsNullOrWhiteSpace(makerUserId))
            throw new ArgumentException("Maker User ID is required.", nameof(makerUserId));
        if (string.IsNullOrWhiteSpace(kycDocumentReference))
            throw new ArgumentException("KYC document verification reference is required.", nameof(kycDocumentReference));

        if (Status is not (AccountOperatingStatus.Dormant or AccountOperatingStatus.Inoperative))
        {
            throw new InvalidOperationException($"Cannot request reactivation for account '{Id.Value}' in state '{Status}'. Account must be 'Dormant' or 'Inoperative'.");
        }

        ReactivationRequestedByUserId = makerUserId.Trim();
        ReactivationKycDocumentReference = kycDocumentReference.Trim();
        ReactivationRequestedAtUtc = DateTime.UtcNow;
        IsReactivationPending = true;
    }

    public void ApproveDormancyReactivation(string branchManagerCheckerUserId, string remarks)
    {
        if (string.IsNullOrWhiteSpace(branchManagerCheckerUserId))
            throw new ArgumentException("Branch Manager User ID is required for dormancy reactivation (BRPD 3.1.20-iv).", nameof(branchManagerCheckerUserId));
        if (string.IsNullOrWhiteSpace(remarks))
            throw new ArgumentException("Reactivation remarks/KYC review reference is required.", nameof(remarks));

        if (Status is not (AccountOperatingStatus.Dormant or AccountOperatingStatus.Inoperative))
        {
            throw new InvalidOperationException($"Cannot reactivate account '{Id.Value}' from status '{Status}'. Account must be 'Dormant' or 'Inoperative'.");
        }

        // HARD LOCK: Dual-Control Maker-Checker duty segregation (BRPD 1.3.18, 10.3.5-x)
        if (IsReactivationPending && branchManagerCheckerUserId.Equals(ReactivationRequestedByUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 3.1.20-iv, 10.3.5-x)",
                $"Maker '{ReactivationRequestedByUserId}' cannot approve their own dormancy reactivation request. Mandatory dual-control segregation.");
        }

        Status = AccountOperatingStatus.Active;
        DormantDate = null;
        ReactivatedByUserId = branchManagerCheckerUserId.Trim();
        ReactivatedAtUtc = DateTime.UtcNow;
        IsReactivationPending = false;
    }

    // Direct reactivation helper for existing tests and backwards-compatibility
    public void ReactivateDormant(string branchManagerUserId, string remarks)
    {
        ApproveDormancyReactivation(branchManagerUserId, remarks);
    }

    // 7. INVARIANT: Leap-Year Actual/365/366 Daily Interest Accrual (BRPD 1.2.2-i, 1.3.7, 3.2.2)
    public Money AccrueDailyInterest(DateOnly businessDate)
    {
        if (AnnualInterestRate <= 0m || Balance.Amount <= 0m || ProductType == DepositProductType.CurrentAccount)
        {
            LastInterestCalculationDate = businessDate;
            return Money.Zero(Balance.Currency);
        }

        // Dynamic leap-year day count (BRPD 3.2.2)
        int daysInYear = DateTime.IsLeapYear(businessDate.Year) ? 366 : 365;
        decimal dailyRate = AnnualInterestRate / (100m * daysInYear);
        decimal dailyInterestAmount = Math.Round(Balance.Amount * dailyRate, 2, MidpointRounding.AwayFromZero);

        var dailyInterest = new Money(dailyInterestAmount, Balance.Currency);
        AccruedInterestProvision += dailyInterest;
        LastInterestCalculationDate = businessDate;

        return dailyInterest;
    }

    // 8. INVARIANT: Periodic Interest Capitalization & Withholding Tax Deductions (BRPD 1.2.2, 3.1.19, 3.2.2)
    public InterestCapitalizationResult CapitalizeInterest(DateOnly businessDate)
    {
        if (AccruedInterestProvision.Amount <= 0m)
        {
            LastInterestCapitalizationDate = businessDate;
            return new InterestCapitalizationResult(
                Money.Zero(Balance.Currency),
                Money.Zero(Balance.Currency),
                Money.Zero(Balance.Currency),
                Money.Zero(Balance.Currency));
        }

        var grossInterest = AccruedInterestProvision;
        var tax = CalculateWithholdingTax(grossInterest);
        var netInterest = grossInterest - tax;

        // Credit net interest to customer balance
        Balance += netInterest;
        if (Balance > HighestCreditBalanceInYear)
        {
            HighestCreditBalanceInYear = Balance;
        }

        // Auto-recover hovering charges from new balance if available
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

        AccruedInterestProvision = Money.Zero(Balance.Currency);
        LastInterestCapitalizationDate = businessDate;

        return new InterestCapitalizationResult(grossInterest, tax, netInterest, recoveredHovering);
    }

    // 9. INVARIANT: Automated NBR Withholding Tax Assessment (BRPD 3.1.19-i)
    // 10% if customer has valid e-TIN; 15% if no e-TIN
    public Money CalculateWithholdingTax(Money grossInterest)
    {
        decimal rate = HasTin ? 0.10m : 0.15m;
        decimal taxAmount = Math.Round(grossInterest.Amount * rate, 2, MidpointRounding.AwayFromZero);
        return new Money(taxAmount, grossInterest.Currency);
    }

    // 10. INVARIANT: Annual NBR Statutory Excise Duty Slab Assessment (BRPD 3.1.19-vii)
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

    // ARCHITECTURAL INTENT: Statutory Service Fee & 15% NBR VAT Deduction for Statements and Certificates (BRPD 3.1.17-i, 3.1.19-iv, 3.1.23-i)
    public void DeductServiceChargeWithVat(
        decimal feeAmount,
        decimal vatRate,
        string narration,
        out Money feeDebited,
        out Money vatDebited)
    {
        if (Status == AccountOperatingStatus.Frozen || Status == AccountOperatingStatus.BlockedDebit)
        {
            throw new InvariantViolationException(
                "AccountDebitBlockedInvariant",
                "BRPD Circular 12/2024 (1.6.2-v, 3.1.15-vi)",
                $"Service charge deduction prohibited on frozen/debit-blocked account {Id.Value}.");
        }

        if (Status != AccountOperatingStatus.Active)
        {
            throw new InvariantViolationException(
                "AccountNotActiveInvariant",
                "BRPD Circular 12/2024 (1.6.4, 3.1.9)",
                $"Service fee cannot be deducted from inactive or closed account {Id.Value}.");
        }

        decimal calculatedVat = Math.Round(feeAmount * vatRate, 2, MidpointRounding.AwayFromZero);
        decimal totalAmount = feeAmount + calculatedVat;
        var requiredMoney = Money.Bdt(totalAmount);

        if (AvailableBalance < requiredMoney)
        {
            // BRPD 3.1.19-viii: Hovering option when balance is insufficient
            AddHoveringCharge(requiredMoney);
            feeDebited = Money.Zero(Balance.Currency);
            vatDebited = Money.Zero(Balance.Currency);
            return;
        }

        feeDebited = Money.Bdt(feeAmount);
        vatDebited = Money.Bdt(calculatedVat);
        Balance -= requiredMoney;
    }

    private static int CalculateMonthsDifference(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate <= fromDate) return 0;
        return ((toDate.Year - fromDate.Year) * 12) + (toDate.Month - fromDate.Month);
    }
}

public record InterestCapitalizationResult(
    Money GrossInterest,
    Money WithholdingTax,
    Money NetInterest,
    Money RecoveredHoveringCharges);
