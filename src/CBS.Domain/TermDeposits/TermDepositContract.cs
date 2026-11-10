using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.TermDeposits;

// ARCHITECTURAL INTENT: Fixed Deposit Receipt (FDR) / Term Deposit lifecycle, Daily Product Accrual,
// Automated Maturity Rollover, Strict CAD Lien Lock, and Premature Encashment Penalty Engine
// under Bangladesh Bank BRPD Circular No. 12 (Module 3.5, 3.6, and 1.3).
public class TermDepositContract : AggregateRoot<string>
{
    public AccountNumber FdrAccountNumber { get; private set; }
    public AccountNumber OperativeAccountNumber { get; private set; }
    public Money PrincipalAmount { get; private set; }
    public int TenorMonths { get; private set; }
    public decimal AnnualInterestRate { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly MaturityDate { get; private set; }
    public TermDepositMaturityInstruction MaturityInstruction { get; private set; }
    public Money AccruedInterestProvision { get; private set; }
    public DateOnly? LastInterestCalculationDate { get; private set; }
    public TermDepositStatus Status { get; private set; }

    // Lien Controls (BRPD 3.5.13)
    public bool IsLienMarked { get; private set; }
    public Money? LienAmount { get; private set; }
    public string? LienReference { get; private set; }

    // Settlement & Premature Encashment History (BRPD 3.5.3)
    public DateOnly? ClosureDate { get; private set; }
    public Money? PaidInterestAmount { get; private set; }
    public Money? WithholdingTaxAmount { get; private set; }
    public decimal? PenalInterestMarkdown { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

#pragma warning disable CS8618
    private TermDepositContract() : base(string.Empty) { }
#pragma warning restore CS8618

    public TermDepositContract(
        string contractId,
        AccountNumber fdrAccountNumber,
        AccountNumber operativeAccountNumber,
        Money principalAmount,
        int tenorMonths,
        decimal annualInterestRate,
        DateOnly startDate,
        TermDepositMaturityInstruction maturityInstruction) : base(contractId)
    {
        if (string.IsNullOrWhiteSpace(contractId))
            throw new ArgumentException("Contract ID is required.", nameof(contractId));
        ArgumentNullException.ThrowIfNull(fdrAccountNumber, nameof(fdrAccountNumber));
        ArgumentNullException.ThrowIfNull(operativeAccountNumber, nameof(operativeAccountNumber));
        ArgumentNullException.ThrowIfNull(principalAmount, nameof(principalAmount));

        if (principalAmount.Amount <= 0m)
        {
            throw new InvariantViolationException(
                "PositiveFdrPrincipalInvariant",
                "BRPD Circular 12/2024 (3.5.1)",
                "FDR principal amount must be positive.");
        }

        if (tenorMonths < 3)
        {
            throw new InvariantViolationException(
                "MinimumFdrTenorInvariant",
                "BRPD Circular 12/2024 (3.5.7)",
                $"Term Deposit tenor '{tenorMonths}' months is below regulatory minimum of 3 months.");
        }

        if (annualInterestRate <= 0m)
        {
            throw new InvariantViolationException(
                "PositiveFdrInterestRateInvariant",
                "BRPD Circular 12/2024 (3.5.7)",
                "Term deposit annual interest rate must be strictly positive.");
        }

        FdrAccountNumber = fdrAccountNumber;
        OperativeAccountNumber = operativeAccountNumber;
        PrincipalAmount = principalAmount;
        TenorMonths = tenorMonths;
        AnnualInterestRate = annualInterestRate;
        StartDate = startDate;
        MaturityDate = startDate.AddMonths(tenorMonths);
        MaturityInstruction = maturityInstruction;
        AccruedInterestProvision = Money.Zero(principalAmount.Currency);
        Status = TermDepositStatus.Active;
        IsLienMarked = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    // 1. INVARIANT: Daily Product Accrual (BRPD 3.5.8, 3.5.11)
    // Formula: (Principal * Days * Rate) / (360 * 100)
    public Money AccrueDailyInterest(DateOnly calculationDate)
    {
        if (Status != TermDepositStatus.Active && Status != TermDepositStatus.LienMarked)
            return Money.Zero(PrincipalAmount.Currency);

        DateOnly fromDate = LastInterestCalculationDate ?? StartDate;
        if (calculationDate <= fromDate)
            return Money.Zero(PrincipalAmount.Currency);

        int days = calculationDate.DayNumber - fromDate.DayNumber;
        decimal dailyInterestAmount = Math.Round((PrincipalAmount.Amount * AnnualInterestRate * days) / 36000m, 2, MidpointRounding.AwayFromZero);

        var accrued = new Money(dailyInterestAmount, PrincipalAmount.Currency);
        AccruedInterestProvision += accrued;
        LastInterestCalculationDate = calculationDate;
        return accrued;
    }

    // 2. INVARIANT: CAD Active Lien Invariant Lock (BRPD 3.5.13)
    public void MarkLien(Money loanAmount, string loanReference)
    {
        if (Status != TermDepositStatus.Active)
            throw new InvalidOperationException($"Cannot mark lien on FDR contract in '{Status}' state.");

        ArgumentNullException.ThrowIfNull(loanAmount, nameof(loanAmount));
        if (string.IsNullOrWhiteSpace(loanReference))
            throw new ArgumentException("Loan reference is required for lien marking.", nameof(loanReference));

        IsLienMarked = true;
        LienAmount = loanAmount;
        LienReference = loanReference;
        Status = TermDepositStatus.LienMarked;
    }

    public void ReleaseLien(string releaseReference)
    {
        if (!IsLienMarked)
            throw new InvalidOperationException($"FDR contract '{Id}' is not under active lien.");

        IsLienMarked = false;
        LienAmount = null;
        LienReference = null;
        Status = TermDepositStatus.Active;
    }

    // 3. INVARIANT: Premature Encashment & Penalty Engine (BRPD 3.5.3, 3110-b)
    public PrematureEncashmentResult CalculateAndApplyPrematureEncashment(
        DateOnly encashmentDate,
        decimal penaltyMarkdownPercentage,
        bool hasTin)
    {
        // HARD LOCK: Active Lien prohibits premature encashment or closure (BRPD 3.5.13)
        if (IsLienMarked)
        {
            throw new InvariantViolationException(
                "FdrActiveLienBlockInvariant",
                "BRPD Circular 12/2024 (3.5.13)",
                $"FDR Contract '{Id}' is under active lien ({LienReference}: {LienAmount}). Premature encashment strictly prohibited until CAD lien release.");
        }

        if (Status != TermDepositStatus.Active)
        {
            throw new InvalidOperationException($"Cannot execute premature encashment on FDR in '{Status}' state.");
        }

        int daysElapsed = Math.Max(0, encashmentDate.DayNumber - StartDate.DayNumber);

        decimal grossInterest = 0m;
        decimal appliedRate = 0m;

        // BRPD 3.5.3: Tenor < 3 months (90 days) yields ZERO interest
        if (daysElapsed >= 90)
        {
            appliedRate = Math.Max(0m, AnnualInterestRate - penaltyMarkdownPercentage);
            grossInterest = Math.Round((PrincipalAmount.Amount * appliedRate * daysElapsed) / 36000m, 2, MidpointRounding.AwayFromZero);
        }

        // NBR Statutory Withholding Tax: 10% with TIN, 15% without TIN (NBR Tax Act 2023 & BRPD Circular 12)
        decimal taxRate = hasTin ? 0.10m : 0.15m;
        decimal taxAmount = Math.Round(grossInterest * taxRate, 2, MidpointRounding.AwayFromZero);
        decimal netInterest = grossInterest - taxAmount;
        decimal totalPayout = PrincipalAmount.Amount + netInterest;

        // Reversal of provision: unearned accrued interest reversed back to P&L
        decimal unearnedProvisionReversal = Math.Max(0m, AccruedInterestProvision.Amount - grossInterest);

        Status = TermDepositStatus.PrematurelyClosed;
        ClosureDate = encashmentDate;
        PaidInterestAmount = new Money(netInterest, PrincipalAmount.Currency);
        WithholdingTaxAmount = new Money(taxAmount, PrincipalAmount.Currency);
        PenalInterestMarkdown = penaltyMarkdownPercentage;

        return new PrematureEncashmentResult(
            ContractId: Id,
            DaysElapsed: daysElapsed,
            AppliedInterestRate: appliedRate,
            GrossInterest: grossInterest,
            WithholdingTax: taxAmount,
            NetInterest: netInterest,
            PrincipalPayout: PrincipalAmount.Amount,
            TotalPayoutAmount: totalPayout,
            ProvisionReversalAmount: unearnedProvisionReversal);
    }

    // 4. INVARIANT: Maturity Rollover & Auto-Renewal Engine (BRPD 3.5.5, 3.5.10)
    public MaturityRolloverResult ExecuteMaturityRollover(decimal prevailingInterestRate, bool hasTin)
    {
        if (Status != TermDepositStatus.Active && Status != TermDepositStatus.LienMarked)
        {
            throw new InvalidOperationException($"Cannot execute maturity rollover on FDR in '{Status}' state.");
        }

        // Full term interest
        int totalDays = MaturityDate.DayNumber - StartDate.DayNumber;
        decimal grossInterest = Math.Round((PrincipalAmount.Amount * AnnualInterestRate * totalDays) / 36000m, 2, MidpointRounding.AwayFromZero);

        decimal taxRate = hasTin ? 0.10m : 0.15m;
        decimal taxAmount = Math.Round(grossInterest * taxRate, 2, MidpointRounding.AwayFromZero);
        decimal netInterest = grossInterest - taxAmount;

        if (MaturityInstruction == TermDepositMaturityInstruction.AutoRenewPrincipalAndInterest)
        {
            // BRPD 3.5.10-ii: Capitalize net interest into new principal
            var newPrincipal = PrincipalAmount + new Money(netInterest, PrincipalAmount.Currency);
            StartDate = MaturityDate;
            MaturityDate = StartDate.AddMonths(TenorMonths);
            PrincipalAmount = newPrincipal;
            AnnualInterestRate = prevailingInterestRate;
            AccruedInterestProvision = Money.Zero(PrincipalAmount.Currency);
            LastInterestCalculationDate = StartDate;
            Status = IsLienMarked ? TermDepositStatus.LienMarked : TermDepositStatus.MaturedRenewed;

            return new MaturityRolloverResult(
                ContractId: Id,
                Instruction: MaturityInstruction,
                RolledPrincipal: newPrincipal.Amount,
                NetInterestCapitalized: netInterest,
                WithholdingTaxDeducted: taxAmount,
                PushedToOperativeAccount: 0m,
                NewMaturityDate: MaturityDate,
                NewInterestRate: prevailingInterestRate);
        }
        else if (MaturityInstruction == TermDepositMaturityInstruction.AutoRenewPrincipalOnly)
        {
            // Roll principal only, net interest credited to operative account
            StartDate = MaturityDate;
            MaturityDate = StartDate.AddMonths(TenorMonths);
            AnnualInterestRate = prevailingInterestRate;
            AccruedInterestProvision = Money.Zero(PrincipalAmount.Currency);
            LastInterestCalculationDate = StartDate;
            Status = IsLienMarked ? TermDepositStatus.LienMarked : TermDepositStatus.MaturedRenewed;

            return new MaturityRolloverResult(
                ContractId: Id,
                Instruction: MaturityInstruction,
                RolledPrincipal: PrincipalAmount.Amount,
                NetInterestCapitalized: 0m,
                WithholdingTaxDeducted: taxAmount,
                PushedToOperativeAccount: netInterest,
                NewMaturityDate: MaturityDate,
                NewInterestRate: prevailingInterestRate);
        }
        else
        {
            // Credit total (principal + net interest) to operative account and close
            Status = TermDepositStatus.ClosedOnMaturity;
            ClosureDate = MaturityDate;
            PaidInterestAmount = new Money(netInterest, PrincipalAmount.Currency);
            WithholdingTaxAmount = new Money(taxAmount, PrincipalAmount.Currency);

            return new MaturityRolloverResult(
                ContractId: Id,
                Instruction: MaturityInstruction,
                RolledPrincipal: 0m,
                NetInterestCapitalized: 0m,
                WithholdingTaxDeducted: taxAmount,
                PushedToOperativeAccount: PrincipalAmount.Amount + netInterest,
                NewMaturityDate: MaturityDate,
                NewInterestRate: 0m);
        }
    }
}

public sealed record PrematureEncashmentResult(
    string ContractId,
    int DaysElapsed,
    decimal AppliedInterestRate,
    decimal GrossInterest,
    decimal WithholdingTax,
    decimal NetInterest,
    decimal PrincipalPayout,
    decimal TotalPayoutAmount,
    decimal ProvisionReversalAmount);

public sealed record MaturityRolloverResult(
    string ContractId,
    TermDepositMaturityInstruction Instruction,
    decimal RolledPrincipal,
    decimal NetInterestCapitalized,
    decimal WithholdingTaxDeducted,
    decimal PushedToOperativeAccount,
    DateOnly NewMaturityDate,
    decimal NewInterestRate);
