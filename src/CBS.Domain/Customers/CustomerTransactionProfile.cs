using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Customers;

// ARCHITECTURAL INTENT: Customer Transaction Profile (TP) Variance & Velocity Alert Engine.
// Enforces mandatory monthly limits across Cash, Transfer, and ADC channels per Bangladesh Bank
// BRPD Circular 12/2024 Module 8.1.9, 7.2.1-v, 8.1.18-f and BFIU AML/CFT regulations.
// Automated breach trigger at > 150% variance demands Enhanced Due Diligence (EDD) review.

public sealed class CustomerTransactionProfile : AggregateRoot<string>
{
    public string CifNumber => Id;
    public Money MonthlyEstimatedTurnover { get; private set; } = null!;
    public Money MaxMonthlyCashDepositAmount { get; private set; } = null!;
    public int MaxMonthlyCashDepositCount { get; private set; }
    public Money MaxMonthlyCashWithdrawalAmount { get; private set; } = null!;
    public int MaxMonthlyCashWithdrawalCount { get; private set; }
    public Money MaxMonthlyTransferAmount { get; private set; } = null!;
    public int MaxMonthlyTransferCount { get; private set; }
    public Money MaxMonthlyAdcAmount { get; private set; } = null!;
    public int MaxMonthlyAdcCount { get; private set; }

    // Real-Time Month Accumulator
    public string CurrentMonthYear { get; private set; } = string.Empty;
    public Money CurrentMonthCashDepositAmount { get; private set; } = null!;
    public int CurrentMonthCashDepositCount { get; private set; }
    public Money CurrentMonthCashWithdrawalAmount { get; private set; } = null!;
    public int CurrentMonthCashWithdrawalCount { get; private set; }
    public Money CurrentMonthTransferAmount { get; private set; } = null!;
    public int CurrentMonthTransferCount { get; private set; }
    public Money CurrentMonthAdcAmount { get; private set; } = null!;
    public int CurrentMonthAdcCount { get; private set; }

    // Variance & Compliance Alert Fields
    public bool IsTpBreached { get; private set; }
    public decimal MaxVariancePercentage { get; private set; }
    public string? LastBreachReason { get; private set; }
    public DateTime LastAssessedAtUtc { get; private set; }
    public string? LastReviewedByUserId { get; private set; }
    public DateTime? LastReviewedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private CustomerTransactionProfile() : base(string.Empty) { }
    #pragma warning restore CS8618

    public CustomerTransactionProfile(
        string cifNumber,
        Money monthlyEstimatedTurnover,
        Money maxMonthlyCashDepositAmount,
        int maxMonthlyCashDepositCount,
        Money maxMonthlyCashWithdrawalAmount,
        int maxMonthlyCashWithdrawalCount,
        Money maxMonthlyTransferAmount,
        int maxMonthlyTransferCount,
        Money maxMonthlyAdcAmount,
        int maxMonthlyAdcCount,
        DateTime initialDate) : base(cifNumber.Trim().ToUpperInvariant())
    {
        if (string.IsNullOrWhiteSpace(cifNumber))
            throw new ArgumentException("Customer CIF is required.", nameof(cifNumber));

        ArgumentNullException.ThrowIfNull(monthlyEstimatedTurnover, nameof(monthlyEstimatedTurnover));
        ArgumentNullException.ThrowIfNull(maxMonthlyCashDepositAmount, nameof(maxMonthlyCashDepositAmount));
        ArgumentNullException.ThrowIfNull(maxMonthlyCashWithdrawalAmount, nameof(maxMonthlyCashWithdrawalAmount));
        ArgumentNullException.ThrowIfNull(maxMonthlyTransferAmount, nameof(maxMonthlyTransferAmount));
        ArgumentNullException.ThrowIfNull(maxMonthlyAdcAmount, nameof(maxMonthlyAdcAmount));

        if (monthlyEstimatedTurnover.Amount <= 0)
        {
            throw new InvariantViolationException(
                "TransactionProfileLimitInvariant",
                "BRPD Circular 12/2024 (8.1.9)",
                "Monthly estimated turnover must be strictly greater than zero.");
        }

        MonthlyEstimatedTurnover = monthlyEstimatedTurnover;
        MaxMonthlyCashDepositAmount = maxMonthlyCashDepositAmount;
        MaxMonthlyCashDepositCount = Math.Max(1, maxMonthlyCashDepositCount);
        MaxMonthlyCashWithdrawalAmount = maxMonthlyCashWithdrawalAmount;
        MaxMonthlyCashWithdrawalCount = Math.Max(1, maxMonthlyCashWithdrawalCount);
        MaxMonthlyTransferAmount = maxMonthlyTransferAmount;
        MaxMonthlyTransferCount = Math.Max(1, maxMonthlyTransferCount);
        MaxMonthlyAdcAmount = maxMonthlyAdcAmount;
        MaxMonthlyAdcCount = Math.Max(1, maxMonthlyAdcCount);

        CurrentMonthYear = initialDate.ToString("yyyy-MM");
        CurrentMonthCashDepositAmount = Money.Bdt(0);
        CurrentMonthCashDepositCount = 0;
        CurrentMonthCashWithdrawalAmount = Money.Bdt(0);
        CurrentMonthCashWithdrawalCount = 0;
        CurrentMonthTransferAmount = Money.Bdt(0);
        CurrentMonthTransferCount = 0;
        CurrentMonthAdcAmount = Money.Bdt(0);
        CurrentMonthAdcCount = 0;

        IsTpBreached = false;
        MaxVariancePercentage = 0m;
        LastBreachReason = null;
        LastAssessedAtUtc = initialDate;
    }

    /// <summary>
    /// Records a financial transaction against the customer's monthly transaction profile,
    /// automatically evaluating cumulative channel totals and triggering variance alerts (>150%).
    /// </summary>
    public void RecordTransaction(TpTransactionCategory category, Money amount, DateTime transactionDate)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        if (amount.Amount <= 0)
            throw new ArgumentException("Transaction amount must be strictly positive.", nameof(amount));

        string targetMonthYear = transactionDate.ToString("yyyy-MM");
        if (targetMonthYear != CurrentMonthYear)
        {
            // Auto roll-over to new calendar month
            CurrentMonthYear = targetMonthYear;
            CurrentMonthCashDepositAmount = Money.Bdt(0);
            CurrentMonthCashDepositCount = 0;
            CurrentMonthCashWithdrawalAmount = Money.Bdt(0);
            CurrentMonthCashWithdrawalCount = 0;
            CurrentMonthTransferAmount = Money.Bdt(0);
            CurrentMonthTransferCount = 0;
            CurrentMonthAdcAmount = Money.Bdt(0);
            CurrentMonthAdcCount = 0;
            IsTpBreached = false;
            MaxVariancePercentage = 0m;
            LastBreachReason = null;
        }

        switch (category)
        {
            case TpTransactionCategory.CashDeposit:
                CurrentMonthCashDepositAmount = CurrentMonthCashDepositAmount + amount;
                CurrentMonthCashDepositCount++;
                break;
            case TpTransactionCategory.CashWithdrawal:
                CurrentMonthCashWithdrawalAmount = CurrentMonthCashWithdrawalAmount + amount;
                CurrentMonthCashWithdrawalCount++;
                break;
            case TpTransactionCategory.ClearingTransferDeposit:
            case TpTransactionCategory.ClearingTransferWithdrawal:
            case TpTransactionCategory.RemittanceInward:
            case TpTransactionCategory.RemittanceOutward:
                CurrentMonthTransferAmount = CurrentMonthTransferAmount + amount;
                CurrentMonthTransferCount++;
                break;
            case TpTransactionCategory.AdcDigitalDebit:
            case TpTransactionCategory.AdcDigitalCredit:
                CurrentMonthAdcAmount = CurrentMonthAdcAmount + amount;
                CurrentMonthAdcCount++;
                break;
        }

        LastAssessedAtUtc = transactionDate;
        EvaluateVariance();
    }

    private void EvaluateVariance()
    {
        decimal maxVar = 0m;
        string? reason = null;

        // Check Total Monthly Turnover (Total Deposits + Inward Transfers + ADC Credits)
        decimal totalInflow = CurrentMonthCashDepositAmount.Amount + CurrentMonthTransferAmount.Amount + CurrentMonthAdcAmount.Amount;
        if (MonthlyEstimatedTurnover.Amount > 0 && totalInflow > MonthlyEstimatedTurnover.Amount * 1.50m)
        {
            decimal varPct = ((totalInflow / MonthlyEstimatedTurnover.Amount) - 1.0m) * 100m;
            if (varPct > maxVar)
            {
                maxVar = varPct;
                reason = $"Monthly total volume ({totalInflow:N2} BDT) exceeded declared turnover ({MonthlyEstimatedTurnover.Amount:N2} BDT) by {varPct:N1}%.";
            }
        }

        // Check Cash Deposit Amount
        if (MaxMonthlyCashDepositAmount.Amount > 0 && CurrentMonthCashDepositAmount.Amount > MaxMonthlyCashDepositAmount.Amount * 1.50m)
        {
            decimal varPct = ((CurrentMonthCashDepositAmount.Amount / MaxMonthlyCashDepositAmount.Amount) - 1.0m) * 100m;
            if (varPct > maxVar)
            {
                maxVar = varPct;
                reason = $"Cash deposits ({CurrentMonthCashDepositAmount.Amount:N2} BDT) exceeded declared cash limit ({MaxMonthlyCashDepositAmount.Amount:N2} BDT) by {varPct:N1}%.";
            }
        }

        // Check Cash Withdrawal Amount
        if (MaxMonthlyCashWithdrawalAmount.Amount > 0 && CurrentMonthCashWithdrawalAmount.Amount > MaxMonthlyCashWithdrawalAmount.Amount * 1.50m)
        {
            decimal varPct = ((CurrentMonthCashWithdrawalAmount.Amount / MaxMonthlyCashWithdrawalAmount.Amount) - 1.0m) * 100m;
            if (varPct > maxVar)
            {
                maxVar = varPct;
                reason = $"Cash withdrawals ({CurrentMonthCashWithdrawalAmount.Amount:N2} BDT) exceeded declared limit ({MaxMonthlyCashWithdrawalAmount.Amount:N2} BDT) by {varPct:N1}%.";
            }
        }

        // Check ADC Digital Amount
        if (MaxMonthlyAdcAmount.Amount > 0 && CurrentMonthAdcAmount.Amount > MaxMonthlyAdcAmount.Amount * 1.50m)
        {
            decimal varPct = ((CurrentMonthAdcAmount.Amount / MaxMonthlyAdcAmount.Amount) - 1.0m) * 100m;
            if (varPct > maxVar)
            {
                maxVar = varPct;
                reason = $"ADC digital volume ({CurrentMonthAdcAmount.Amount:N2} BDT) exceeded declared limit ({MaxMonthlyAdcAmount.Amount:N2} BDT) by {varPct:N1}%.";
            }
        }

        if (maxVar > 0m)
        {
            IsTpBreached = true;
            MaxVariancePercentage = Math.Round(maxVar, 2);
            LastBreachReason = reason;
        }
    }

    /// <summary>
    /// Updates declared TP limits upon verified customer request and compliance approval.
    /// </summary>
    public void UpdateDeclaredLimits(
        Money monthlyTurnover,
        Money maxCashDep,
        int maxCashDepCount,
        Money maxCashWdl,
        int maxCashWdlCount,
        Money maxTransfer,
        int maxTransferCount,
        Money maxAdc,
        int maxAdcCount,
        string updatedByUserId)
    {
        ArgumentNullException.ThrowIfNull(monthlyTurnover, nameof(monthlyTurnover));
        if (string.IsNullOrWhiteSpace(updatedByUserId))
            throw new ArgumentException("Updating user ID is required.", nameof(updatedByUserId));

        if (monthlyTurnover.Amount <= 0)
        {
            throw new InvariantViolationException(
                "TransactionProfileLimitInvariant",
                "BRPD Circular 12/2024 (8.1.9)",
                "Monthly estimated turnover must be strictly greater than zero.");
        }

        MonthlyEstimatedTurnover = monthlyTurnover;
        MaxMonthlyCashDepositAmount = maxCashDep;
        MaxMonthlyCashDepositCount = Math.Max(1, maxCashDepCount);
        MaxMonthlyCashWithdrawalAmount = maxCashWdl;
        MaxMonthlyCashWithdrawalCount = Math.Max(1, maxCashWdlCount);
        MaxMonthlyTransferAmount = maxTransfer;
        MaxMonthlyTransferCount = Math.Max(1, maxTransferCount);
        MaxMonthlyAdcAmount = maxAdc;
        MaxMonthlyAdcCount = Math.Max(1, maxAdcCount);

        LastReviewedByUserId = updatedByUserId.Trim();
        LastReviewedAtUtc = DateTime.UtcNow;

        // Re-evaluate current month against new limits
        EvaluateVariance();
    }

    /// <summary>
    /// Clears the TP breach flag following Enhanced Due Diligence (EDD) documentation and Senior Management Sign-off.
    /// </summary>
    public void ClearBreachAfterEdd(string reviewedByUserId, string justification)
    {
        if (string.IsNullOrWhiteSpace(reviewedByUserId))
            throw new ArgumentException("Reviewing user ID is required.", nameof(reviewedByUserId));
        if (string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("EDD review justification is required.", nameof(justification));

        IsTpBreached = false;
        LastBreachReason = $"[Cleared after EDD]: {justification.Trim()}";
        LastReviewedByUserId = reviewedByUserId.Trim();
        LastReviewedAtUtc = DateTime.UtcNow;
    }
}
