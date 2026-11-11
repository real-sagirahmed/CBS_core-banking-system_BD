using CBS.Domain.Common;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Deposits.Multiplication;

// ARCHITECTURAL INTENT: Deposit Multiplication Schemes (Double, Triple, etc.)
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 3.7.
// Enforces mathematical tenor derivation based on interest rate, daily product accrual,
// and statutory prohibition of inter-branch scheme transfers (BRPD 3.7.5).
public class DepositMultiplicationAccount : AggregateRoot<AccountNumber>
{
    public CifNumber Cif { get; private set; }
    public string BranchCode { get; private set; }
    public Money InitialDeposit { get; private set; }
    public int MultiplierFactor { get; private set; } // 2 for Double, 3 for Triple
    public decimal AnnualInterestRate { get; private set; }
    public int CalculatedTenorMonths { get; private set; }
    public Money MaturityAmount { get; private set; }
    public DateOnly OpenDate { get; private set; }
    public DateOnly MaturityDate { get; private set; }
    public bool IsClosed { get; private set; }
    public DateOnly? ClosedDate { get; private set; }

    #pragma warning disable CS8618
    protected DepositMultiplicationAccount() : base(null!) { }
    #pragma warning restore CS8618

    public DepositMultiplicationAccount(
        AccountNumber accountNumber,
        CifNumber cif,
        string branchCode,
        Money initialDeposit,
        int multiplierFactor,
        decimal annualInterestRate,
        DateOnly openDate) : base(accountNumber)
    {
        ArgumentNullException.ThrowIfNull(accountNumber, nameof(accountNumber));
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));
        ArgumentNullException.ThrowIfNull(initialDeposit, nameof(initialDeposit));

        if (string.IsNullOrWhiteSpace(branchCode))
            throw new ArgumentException("Branch code is required.", nameof(branchCode));
        if (multiplierFactor < 2 || multiplierFactor > 5)
            throw new ArgumentOutOfRangeException(nameof(multiplierFactor), "Deposit multiplication factor must be between 2 and 5.");
        if (annualInterestRate <= 0 || annualInterestRate > 25)
            throw new ArgumentOutOfRangeException(nameof(annualInterestRate), "Annual interest rate must be between 0% and 25%.");
        if (initialDeposit.Amount <= 0)
            throw new ArgumentException("Initial deposit amount must be positive.", nameof(initialDeposit));

        Cif = cif;
        BranchCode = branchCode.Trim();
        InitialDeposit = initialDeposit;
        MultiplierFactor = multiplierFactor;
        AnnualInterestRate = annualInterestRate;
        OpenDate = openDate;

        // Mathematical Tenor Derivation: ln(Multiplier) / ln(1 + Rate/1200)
        double monthlyRate = (double)(annualInterestRate / 1200m);
        double monthsRequired = Math.Log(multiplierFactor) / Math.Log(1.0 + monthlyRate);
        CalculatedTenorMonths = (int)Math.Ceiling(monthsRequired);

        MaturityDate = openDate.AddMonths(CalculatedTenorMonths);
        MaturityAmount = new Money(initialDeposit.Amount * multiplierFactor, initialDeposit.Currency);
        IsClosed = false;
    }

    // 1. INVARIANT: Strict Prohibition of Inter-Branch Scheme Transfer (BRPD 3.7.5-i)
    public void TransferToOtherBranch(string destinationBranchCode)
    {
        throw new InvariantViolationException(
            "MultiplicationSchemeTransferProhibitedInvariant",
            "BRPD Circular 12/2024 (3.7.5-i)",
            $"Transfer of fixed deposit multiplication schemes between branches is prohibited. Account '{Id.Value}' must remain at branch '{BranchCode}'.");
    }

    // 2. INVARIANT: Maturity or Premature Encashment (BRPD 3.7.2)
    public Money Encash(DateOnly encashmentDate, decimal prematurePenaltyRate = 0m)
    {
        if (IsClosed)
        {
            throw new InvariantViolationException(
                "SchemeAlreadyClosedInvariant",
                "BRPD Circular 12/2024 (3.7.2)",
                $"Multiplication account '{Id.Value}' is already encashed and closed.");
        }

        Money payoutAmount;
        if (encashmentDate >= MaturityDate)
        {
            // Full maturity payout
            payoutAmount = MaturityAmount;
        }
        else
        {
            // Premature encashment: Simple interest calculation with penalty
            int daysActive = Math.Max(0, encashmentDate.DayNumber - OpenDate.DayNumber);
            decimal applicableRate = Math.Max(0m, AnnualInterestRate - prematurePenaltyRate);
            decimal prematureInterest = InitialDeposit.Amount * (applicableRate / 100m) * (daysActive / 365m);
            prematureInterest = Math.Round(prematureInterest, InitialDeposit.Currency.DecimalPlaces, MidpointRounding.AwayFromZero);
            payoutAmount = InitialDeposit + new Money(prematureInterest, InitialDeposit.Currency);
        }

        IsClosed = true;
        ClosedDate = encashmentDate;
        return payoutAmount;
    }
}
