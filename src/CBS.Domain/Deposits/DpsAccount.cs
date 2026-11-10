using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Deposits;

// ARCHITECTURAL INTENT: Deposit Pension Scheme (DPS) & Recurring Deposit Engine (BRPD Circular 12/2024 Module 3.7 & 3.8).
// Enforces linked CASA account auto-debit sweeps, grace period late fine calculation,
// 4-strike consecutive default auto-suspension, premature encashment penalties with NBR withholding tax,
// and CAD active lien lock preventing encashment or liquidation while encumbered as loan collateral.
public class DpsAccount : AggregateRoot<string>
{
    public AccountNumber DpsAccountNumber { get; private set; }
    public AccountNumber LinkedCasaAccountNumber { get; private set; }
    public CifNumber CustomerCif { get; private set; }
    public Money MonthlyInstallmentAmount { get; private set; }
    public int TenorYears { get; private set; }
    public int TotalInstallments => TenorYears * 12;
    public decimal ContractedAnnualRate { get; private set; }
    public decimal PrematureSavingsPenaltyRate { get; private set; }
    public int DebitDayOfMonth { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly MaturityDate { get; private set; }
    public Money TotalPrincipalPaid { get; private set; }
    public Money TotalInterestAccrued { get; private set; }
    public int PaidInstallmentsCount { get; private set; }
    public int ConsecutiveDefaultCount { get; private set; }
    public DpsStatus Status { get; private set; }
    public DpsSettlementMode SettlementMode { get; private set; }
    public bool IsLienMarked { get; private set; }
    public string? ActiveLienFacilityId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    private readonly List<DpsInstallmentSchedule> _installments = [];
    public IReadOnlyCollection<DpsInstallmentSchedule> Installments => _installments.AsReadOnly();

#pragma warning disable CS8618
    private DpsAccount() : base(string.Empty) { }
#pragma warning restore CS8618

    public DpsAccount(
        string id,
        AccountNumber dpsAccountNumber,
        AccountNumber linkedCasaAccountNumber,
        CifNumber customerCif,
        Money monthlyInstallmentAmount,
        int tenorYears,
        decimal contractedAnnualRate,
        decimal prematureSavingsPenaltyRate,
        int debitDayOfMonth,
        DateOnly startDate,
        DpsSettlementMode settlementMode = DpsSettlementMode.LumpSumCreditToCasa) : base(id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("DPS account ID is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(dpsAccountNumber);
        ArgumentNullException.ThrowIfNull(linkedCasaAccountNumber);
        ArgumentNullException.ThrowIfNull(customerCif);
        ArgumentNullException.ThrowIfNull(monthlyInstallmentAmount);

        if (monthlyInstallmentAmount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "DpsInstallmentAmountInvariant",
                "BRPD Circular 12/2024 (3.8.2)",
                "DPS monthly installment amount must be positive.");
        }

        if (tenorYears is < 1 or > 15)
        {
            throw new InvariantViolationException(
                "DpsTenorInvariant",
                "BRPD Circular 12/2024 (3.8.1)",
                $"DPS tenor '{tenorYears}' years is invalid. Permitted tenors are between 1 and 15 years.");
        }

        if (contractedAnnualRate <= 0)
        {
            throw new InvariantViolationException(
                "DpsInterestRateInvariant",
                "BRPD Circular 12/2024 (3.8.4)",
                "Contracted interest rate must be greater than zero.");
        }

        if (debitDayOfMonth is < 1 or > 28)
        {
            throw new InvariantViolationException(
                "DebitDayOfMonthInvariant",
                "BRPD Circular 12/2024 (3.8.3)",
                $"Debit day of month '{debitDayOfMonth}' must be between 1 and 28 to ensure all months accommodate it.");
        }

        Id = id;
        DpsAccountNumber = dpsAccountNumber;
        LinkedCasaAccountNumber = linkedCasaAccountNumber;
        CustomerCif = customerCif;
        MonthlyInstallmentAmount = monthlyInstallmentAmount;
        TenorYears = tenorYears;
        ContractedAnnualRate = contractedAnnualRate;
        PrematureSavingsPenaltyRate = prematureSavingsPenaltyRate;
        DebitDayOfMonth = debitDayOfMonth;
        StartDate = startDate;
        MaturityDate = startDate.AddYears(tenorYears);
        TotalPrincipalPaid = new Money(0, monthlyInstallmentAmount.Currency);
        TotalInterestAccrued = new Money(0, monthlyInstallmentAmount.Currency);
        PaidInstallmentsCount = 0;
        ConsecutiveDefaultCount = 0;
        Status = DpsStatus.Active;
        SettlementMode = settlementMode;
        IsLienMarked = false;
        CreatedAtUtc = DateTime.UtcNow;

        GenerateSchedule();
    }

    private void GenerateSchedule()
    {
        _installments.Clear();
        int total = TenorYears * 12;

        for (int i = 1; i <= total; i++)
        {
            // Monthly installment due on DebitDayOfMonth of consecutive months
            DateOnly rawDue = StartDate.AddMonths(i - 1);
            int year = rawDue.Year;
            int month = rawDue.Month;
            int daysInMonth = DateTime.DaysInMonth(year, month);
            int day = Math.Min(DebitDayOfMonth, daysInMonth);
            DateOnly dueDate = new(year, month, day);

            string scheduleId = $"{Id}-INS-{i:D3}";
            _installments.Add(new DpsInstallmentSchedule(
                scheduleId,
                Id,
                i,
                dueDate,
                new Money(MonthlyInstallmentAmount.Amount, MonthlyInstallmentAmount.Currency)));
        }
    }

    public void ProcessInstallmentPayment(
        int installmentNumber,
        Money paidAmount,
        DateOnly paymentDate,
        Money? lateFee = null,
        string? voucherNumber = null)
    {
        if (Status != DpsStatus.Active && Status != DpsStatus.LienMarked)
        {
            throw new InvariantViolationException(
                "DpsAccountStatusInvariant",
                "BRPD Circular 12/2024 (3.8.7)",
                $"Cannot process installment payment for DPS account in '{Status}' status.");
        }

        var schedule = _installments.FirstOrDefault(s => s.InstallmentNumber == installmentNumber);
        if (schedule is null)
        {
            throw new InvariantViolationException(
                "InstallmentScheduleNotFound",
                "BRPD Circular 12/2024 (3.8.1)",
                $"Installment number '{installmentNumber}' not found for DPS '{Id}'.");
        }

        if (schedule.Status == DpsInstallmentStatus.Paid)
        {
            throw new InvariantViolationException(
                "InstallmentAlreadyPaidInvariant",
                "BRPD Circular 12/2024 (3.8.3)",
                $"Installment number '{installmentNumber}' is already paid.");
        }

        if (paidAmount.Amount < MonthlyInstallmentAmount.Amount)
        {
            throw new InvariantViolationException(
                "InsufficientInstallmentPaymentInvariant",
                "BRPD Circular 12/2024 (3.8.2)",
                $"Paid amount '{paidAmount.Amount}' is less than required installment amount '{MonthlyInstallmentAmount.Amount}'.");
        }

        schedule.MarkPaid(paymentDate, lateFee ?? new Money(0, MonthlyInstallmentAmount.Currency), voucherNumber);

        TotalPrincipalPaid = TotalPrincipalPaid + MonthlyInstallmentAmount;
        PaidInstallmentsCount++;
        ConsecutiveDefaultCount = 0; // Reset consecutive default counter

        // Compound interest accrual for this installment period
        // Monthly interest = (TotalPrincipalPaid * ContractedAnnualRate) / (12 * 100)
        decimal monthlyRate = ContractedAnnualRate / (12.0m * 100.0m);
        decimal newInterest = Math.Round(TotalPrincipalPaid.Amount * monthlyRate, 2, MidpointRounding.AwayFromZero);
        TotalInterestAccrued = TotalInterestAccrued + new Money(newInterest, MonthlyInstallmentAmount.Currency);

        // Check if fully matured
        if (PaidInstallmentsCount >= TotalInstallments)
        {
            Status = IsLienMarked ? DpsStatus.LienMarked : DpsStatus.Matured;
        }
    }

    public void RecordMissedInstallment(DateOnly evaluationDate)
    {
        if (Status != DpsStatus.Active) return;

        // Check overdue installments where DueDate + 10 days grace period < evaluationDate
        foreach (var installment in _installments.Where(s => s.Status == DpsInstallmentStatus.Pending && s.DueDate.AddDays(10) < evaluationDate))
        {
            // Assess late fee (e.g. 2% of installment amount per missed installment)
            decimal lateFeeAmount = Math.Round(installment.DueAmount.Amount * 0.02m, 2, MidpointRounding.AwayFromZero);
            installment.MarkOverdue(new Money(lateFeeAmount, installment.DueAmount.Currency));
            ConsecutiveDefaultCount++;
        }

        // BRPD 3.8.7: If 4 consecutive installments default, auto-suspend account
        if (ConsecutiveDefaultCount >= 4)
        {
            Status = DpsStatus.DefaultSuspended;
        }
    }

    public void RegularizeSuspendedAccount(MakerCheckerAudit audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        if (audit.Status != MakerCheckerStatus.Approved)
        {
            throw new InvariantViolationException(
                "MakerCheckerApprovalRequired",
                "BRPD Circular 12/2024 (1.3.18, 3.8.7)",
                "Regularization requires dual-control checker approval.");
        }

        if (Status != DpsStatus.DefaultSuspended)
        {
            throw new InvariantViolationException(
                "DpsRegularizationInvariant",
                "BRPD Circular 12/2024 (3.8.7)",
                $"Only 'DefaultSuspended' DPS accounts can be regularized. Current status: '{Status}'.");
        }

        Status = IsLienMarked ? DpsStatus.LienMarked : DpsStatus.Active;
        ConsecutiveDefaultCount = 0;
    }

    public PrematureDpsEncashmentResult CalculatePrematureEncashment(
        DateOnly encashmentDate,
        bool hasTaxIdentificationNumber)
    {
        AssertCanBeLiquidated();

        int monthsCompleted = PaidInstallmentsCount;
        decimal netPrincipal = TotalPrincipalPaid.Amount;
        decimal grossInterest = 0m;

        // BRPD 3.8.5: If closed before 12 months, zero interest is paid
        if (monthsCompleted >= 12)
        {
            // Recalculate interest at premature savings penalty rate (simple or monthly compound on completed installments)
            decimal monthlySavingsRate = PrematureSavingsPenaltyRate / (12.0m * 100.0m);
            decimal accruedSum = 0m;
            decimal runningBalance = 0m;

            for (int m = 1; m <= monthsCompleted; m++)
            {
                runningBalance += MonthlyInstallmentAmount.Amount;
                accruedSum += runningBalance * monthlySavingsRate;
            }

            grossInterest = Math.Round(accruedSum, 2, MidpointRounding.AwayFromZero);
        }

        // NBR Source Tax: 10% if e-TIN provided, 15% if no e-TIN
        decimal taxRate = hasTaxIdentificationNumber ? 0.10m : 0.15m;
        decimal withholdingTax = Math.Round(grossInterest * taxRate, 2, MidpointRounding.AwayFromZero);
        decimal netInterest = grossInterest - withholdingTax;
        decimal netPayable = netPrincipal + netInterest;

        return new PrematureDpsEncashmentResult(
            TotalPrincipal: new Money(netPrincipal, MonthlyInstallmentAmount.Currency),
            GrossInterest: new Money(grossInterest, MonthlyInstallmentAmount.Currency),
            WithholdingTax: new Money(withholdingTax, MonthlyInstallmentAmount.Currency),
            NetPayableAmount: new Money(netPayable, MonthlyInstallmentAmount.Currency),
            EffectiveRateApplied: monthsCompleted < 12 ? 0.00m : PrematureSavingsPenaltyRate,
            MonthsCompleted: monthsCompleted);
    }

    public void MarkLien(string lienFacilityId, string reason)
    {
        if (string.IsNullOrWhiteSpace(lienFacilityId))
            throw new ArgumentException("Lien facility ID is required.", nameof(lienFacilityId));

        if (IsLienMarked)
        {
            throw new InvariantViolationException(
                "DuplicateLienInvariant",
                "BRPD Circular 12/2024 (3.8.10)",
                $"DPS account '{DpsAccountNumber}' already has an active lien '{ActiveLienFacilityId}'.");
        }

        IsLienMarked = true;
        ActiveLienFacilityId = lienFacilityId;
        Status = DpsStatus.LienMarked;
    }

    public void ReleaseLien(string lienFacilityId)
    {
        if (!IsLienMarked || ActiveLienFacilityId != lienFacilityId)
        {
            throw new InvariantViolationException(
                "LienMismatchInvariant",
                "BRPD Circular 12/2024 (3.8.10)",
                $"Cannot release lien '{lienFacilityId}'. Active lien is '{ActiveLienFacilityId}'.");
        }

        IsLienMarked = false;
        ActiveLienFacilityId = null;
        Status = PaidInstallmentsCount >= TotalInstallments ? DpsStatus.Matured : DpsStatus.Active;
    }

    public void AssertCanBeLiquidated()
    {
        // BRPD Clause 3.8.10 & 3.6.1: Active lien strictly forbids premature liquidation or maturity transfer
        if (IsLienMarked || Status == DpsStatus.LienMarked)
        {
            throw new InvariantViolationException(
                "ActiveLienLockInvariant",
                "BRPD Circular 12/2024 (3.8.10, 3.6.1)",
                $"DPS account '{DpsAccountNumber}' has an active CAD lien ('{ActiveLienFacilityId}'). Premature encashment or payout is strictly prohibited until the credit facility is adjusted.");
        }

        if (Status == DpsStatus.Encashed || Status == DpsStatus.Closed)
        {
            throw new InvariantViolationException(
                "DpsAlreadyClosedInvariant",
                "BRPD Circular 12/2024 (3.8.5)",
                $"DPS account '{DpsAccountNumber}' is already '{Status}'.");
        }
    }

    public void MarkEncashed(DateOnly encashmentDate)
    {
        AssertCanBeLiquidated();
        Status = DpsStatus.Encashed;
        ClosedAtUtc = DateTime.UtcNow;
    }
}

public sealed record PrematureDpsEncashmentResult(
    Money TotalPrincipal,
    Money GrossInterest,
    Money WithholdingTax,
    Money NetPayableAmount,
    decimal EffectiveRateApplied,
    int MonthsCompleted);
