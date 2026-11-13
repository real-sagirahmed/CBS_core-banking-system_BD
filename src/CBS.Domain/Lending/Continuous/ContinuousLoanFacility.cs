// ARCHITECTURAL INTENT: Continuous Loan (Cash Credit / Overdraft - CC/OD) Aggregate Root
// Enforcing dynamic Drawing Power (DP), Stock & Book Debts Statements, Godown Inspection,
// Temporary Overdraft (TOD), Annual Renewal Governance, and Quarterly Interest Capitalization
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 and 3.9.

using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Lending.Continuous;

public class ContinuousLoanFacility : AggregateRoot<AccountNumber>
{
    public CifNumber Cif { get; private set; }
    public string BranchCode { get; private set; }
    public ContinuousLoanCategory Category { get; private set; }
    public string? LinkedDepositAccountNumber { get; private set; }

    public Money SanctionLimit { get; private set; }
    public Money DrawingPower { get; private set; }
    public Money OutstandingPrincipal { get; private set; }

    public Money RegularAccruedInterest { get; private set; }
    public Money OverdueInterest { get; private set; }
    public Money PenalInterest { get; private set; }
    public Money AccruedCharges { get; private set; }
    public Money InterestSuspense { get; private set; }

    public decimal RegularInterestRate { get; private set; }
    public decimal PenalInterestRate { get; private set; }

    public DateOnly SanctionDate { get; private set; }
    public DateOnly ExpiryDate { get; private set; }
    public DateOnly? LastRenewalDate { get; private set; }
    public int RenewalCount { get; private set; }
    public ContinuousLoanRenewalStatus RenewalStatus { get; private set; }

    public decimal StockMarginPercent { get; private set; }
    public decimal BookDebtsMarginPercent { get; private set; }

    public Money? CurrentTodAmount { get; private set; }
    public DateOnly? CurrentTodExpiryDate { get; private set; }
    public decimal? CurrentTodPenalRate { get; private set; }
    public string? CurrentTodApprovedByUserId { get; private set; }

    public DateOnly? LastStockStatementDate { get; private set; }
    public DateOnly? LastInspectionDate { get; private set; }

    public LoanClassificationGrade Classification { get; private set; }
    public int DaysPastDue { get; private set; }
    public bool IsDebitRestricted { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

#pragma warning disable CS8618
    protected ContinuousLoanFacility() : base(null!) { }
#pragma warning restore CS8618

    public ContinuousLoanFacility(
        AccountNumber accountNumber,
        CifNumber cif,
        string branchCode,
        ContinuousLoanCategory category,
        Money sanctionLimit,
        Money initialDrawingPower,
        decimal regularInterestRate,
        decimal penalInterestRate,
        DateOnly sanctionDate,
        DateOnly expiryDate,
        decimal stockMarginPercent = 25.00m,
        decimal bookDebtsMarginPercent = 35.00m,
        string? linkedDepositAccountNumber = null) : base(accountNumber)
    {
        ArgumentNullException.ThrowIfNull(accountNumber, nameof(accountNumber));
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode, nameof(branchCode));
        ArgumentNullException.ThrowIfNull(sanctionLimit, nameof(sanctionLimit));
        ArgumentNullException.ThrowIfNull(initialDrawingPower, nameof(initialDrawingPower));

        if (sanctionLimit.Amount <= 0m)
        {
            throw new InvariantViolationException(
                "InvalidSanctionLimitInvariant",
                "BRPD Circular 12/2024 (4.1.3)",
                "Continuous loan facility sanction limit must be strictly positive.");
        }

        if (initialDrawingPower > sanctionLimit)
        {
            throw new InvariantViolationException(
                "DrawingPowerExceedsSanctionLimitInvariant",
                "BRPD Circular 12/2024 (4.1.6)",
                $"Initial Drawing Power ({initialDrawingPower}) cannot exceed sanctioned facility limit ({sanctionLimit}).");
        }

        if (expiryDate <= sanctionDate)
        {
            throw new ArgumentException("Facility expiry date must be strictly after sanction date.", nameof(expiryDate));
        }

        Cif = cif;
        BranchCode = branchCode.Trim();
        Category = category;
        LinkedDepositAccountNumber = linkedDepositAccountNumber?.Trim();

        SanctionLimit = sanctionLimit;
        DrawingPower = initialDrawingPower;
        OutstandingPrincipal = Money.Zero(Currency.BDT);

        RegularAccruedInterest = Money.Zero(Currency.BDT);
        OverdueInterest = Money.Zero(Currency.BDT);
        PenalInterest = Money.Zero(Currency.BDT);
        AccruedCharges = Money.Zero(Currency.BDT);
        InterestSuspense = Money.Zero(Currency.BDT);

        RegularInterestRate = regularInterestRate;
        PenalInterestRate = penalInterestRate > 0m ? penalInterestRate : regularInterestRate + 2.00m;

        SanctionDate = sanctionDate;
        ExpiryDate = expiryDate;
        LastRenewalDate = null;
        RenewalCount = 0;
        RenewalStatus = ContinuousLoanRenewalStatus.Active;

        StockMarginPercent = stockMarginPercent;
        BookDebtsMarginPercent = bookDebtsMarginPercent;

        CurrentTodAmount = null;
        CurrentTodExpiryDate = null;
        CurrentTodPenalRate = null;
        CurrentTodApprovedByUserId = null;

        LastStockStatementDate = null;
        LastInspectionDate = null;

        Classification = LoanClassificationGrade.Standard;
        DaysPastDue = 0;
        IsDebitRestricted = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void SubmitStockStatement(ContinuousLoanStockStatement statement, Money? insuranceCoverageCap = null)
    {
        ArgumentNullException.ThrowIfNull(statement, nameof(statement));

        var eligibleStockDp = statement.EligibleStockDrawingPower;
        if (insuranceCoverageCap is not null && insuranceCoverageCap.Amount < eligibleStockDp.Amount)
        {
            // BRPD 4.1.3-iv: Insurable stock DP capped by valid insurance sum insured
            eligibleStockDp = insuranceCoverageCap;
        }

        var totalCalculated = eligibleStockDp.Amount + statement.EligibleBookDebtsDrawingPower.Amount;
        var newDp = Math.Min(SanctionLimit.Amount, totalCalculated);

        DrawingPower = Money.Bdt(newDp);
        LastStockStatementDate = statement.StatementDate;
    }

    public void RecordGodownInspection(ContinuousLoanGodownInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection, nameof(inspection));

        LastInspectionDate = inspection.InspectionDate;

        // BRPD 4.1.3-iv: If insurance is invalid or expired, stock value component is zeroed out
        if (!inspection.IsInsuranceValid)
        {
            // Fall back DP strictly to current non-stock components or zero
            var deflatedDp = Math.Min(DrawingPower.Amount, 0m);
            DrawingPower = Money.Bdt(deflatedDp);
        }
    }

    public void GrantTemporaryOverdraft(
        Money amount,
        DateOnly expiryDate,
        decimal penalRate,
        string approvedByUserId,
        string approvalReference)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedByUserId, nameof(approvedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalReference, nameof(approvalReference));

        if (amount.Amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "TOD amount must be positive.");

        if (expiryDate <= DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("TOD expiry date must be strictly in the future.", nameof(expiryDate));

        CurrentTodAmount = amount;
        CurrentTodExpiryDate = expiryDate;
        CurrentTodPenalRate = penalRate;
        CurrentTodApprovedByUserId = approvedByUserId.Trim().ToUpperInvariant();
    }

    public void CheckAndExpireTod(DateOnly asOfDate)
    {
        if (CurrentTodExpiryDate.HasValue && asOfDate > CurrentTodExpiryDate.Value)
        {
            CurrentTodAmount = null;
            CurrentTodExpiryDate = null;
            CurrentTodPenalRate = null;
            CurrentTodApprovedByUserId = null;
        }
    }

    public void CheckAndMarkExpired(DateOnly asOfDate)
    {
        if (asOfDate > ExpiryDate && RenewalStatus != ContinuousLoanRenewalStatus.Expired)
        {
            RenewalStatus = ContinuousLoanRenewalStatus.Expired;
            IsDebitRestricted = true;
        }
    }

    public void Withdraw(
        Money amount,
        string channelOrVoucher,
        string approvedByUserId,
        DateOnly asOfDate,
        bool isOverrideApproved = false)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedByUserId, nameof(approvedByUserId));

        if (amount.Amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Withdrawal amount must be positive.");

        CheckAndExpireTod(asOfDate);
        CheckAndMarkExpired(asOfDate);

        // BRPD 3.9.5-ii: Expired continuous loan debit restriction
        if (RenewalStatus == ContinuousLoanRenewalStatus.Expired && !isOverrideApproved)
        {
            throw new InvariantViolationException(
                "ExpiredContinuousLoanDebitBlockedInvariant",
                "BRPD Circular 12/2024 (3.9.5-ii, 4.1.13)",
                $"Continuous loan facility '{Id.Value}' has expired on {ExpiryDate:yyyy-MM-dd}. Debit withdrawals are strictly prohibited without competent authority override.");
        }

        // BRPD 4.1.6: Calculate effective limit: min(SanctionLimit, DrawingPower) + TOD
        var effectiveBaseLimit = Math.Min(SanctionLimit.Amount, DrawingPower.Amount);
        var effectiveLimitWithTod = effectiveBaseLimit + (CurrentTodAmount?.Amount ?? 0m);

        if ((OutstandingPrincipal.Amount + amount.Amount) > effectiveLimitWithTod)
        {
            throw new InvariantViolationException(
                "DrawingPowerExceededInvariant",
                "BRPD Circular 12/2024 (4.1.6)",
                $"Withdrawal of BDT {amount.Amount:N2} exceeds available drawing ceiling. Outstanding balance would reach BDT {OutstandingPrincipal.Amount + amount.Amount:N2} against effective limit BDT {effectiveLimitWithTod:N2}.");
        }

        OutstandingPrincipal += amount;
    }

    public void Deposit(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));

        if (amount.Amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Deposit amount must be positive.");

        // BRPD 3.9.5-i: Credits are unrestricted even if facility is expired
        var newOutstanding = Math.Max(0m, OutstandingPrincipal.Amount - amount.Amount);
        OutstandingPrincipal = Money.Bdt(newOutstanding);
    }

    public void AccrueDailyInterest(DateOnly asOfDate, int days = 1)
    {
        if (OutstandingPrincipal.Amount <= 0m) return;

        CheckAndExpireTod(asOfDate);
        CheckAndMarkExpired(asOfDate);

        // Use penal interest if expired or in excess over limit
        var applicableRate = (RenewalStatus == ContinuousLoanRenewalStatus.Expired)
            ? PenalInterestRate
            : RegularInterestRate;

        // If TOD active and balance exceeds base limit, apply TOD penal rate on excess
        var baseLimit = Math.Min(SanctionLimit.Amount, DrawingPower.Amount);
        decimal regularPartInterest = 0m;
        decimal excessPartInterest = 0m;

        if (CurrentTodAmount is not null && OutstandingPrincipal.Amount > baseLimit)
        {
            var regularPortion = baseLimit;
            var excessPortion = OutstandingPrincipal.Amount - baseLimit;
            var dailyRegularRate = applicableRate / (100m * 365m);
            var dailyPenalRate = (CurrentTodPenalRate ?? PenalInterestRate) / (100m * 365m);

            regularPartInterest = Math.Round(regularPortion * dailyRegularRate * days, 2, MidpointRounding.AwayFromZero);
            excessPartInterest = Math.Round(excessPortion * dailyPenalRate * days, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            var dailyRate = applicableRate / (100m * 365m);
            regularPartInterest = Math.Round(OutstandingPrincipal.Amount * dailyRate * days, 2, MidpointRounding.AwayFromZero);
        }

        var totalAccrued = regularPartInterest + excessPartInterest;

        // BRPD 3.9.2-i: Interest suspension for classified accounts (SS, DF, BL)
        if (Classification is LoanClassificationGrade.SubStandard_SS
            or LoanClassificationGrade.Doubtful_DF
            or LoanClassificationGrade.BadLoss_BL)
        {
            InterestSuspense += Money.Bdt(totalAccrued);
        }
        else
        {
            RegularAccruedInterest += Money.Bdt(regularPartInterest);
            if (excessPartInterest > 0m)
            {
                PenalInterest += Money.Bdt(excessPartInterest);
            }
        }
    }

    public void CapitalizeQuarterlyInterest(DateOnly quarterEndDate)
    {
        // HARD GATE: BRPD 3.9.2-ii strictly prohibits interest capitalization for NPA / classified accounts
        if (Classification is LoanClassificationGrade.SubStandard_SS
            or LoanClassificationGrade.Doubtful_DF
            or LoanClassificationGrade.BadLoss_BL)
        {
            throw new InvariantViolationException(
                "NpaInterestCapitalizationForbiddenInvariant",
                "BRPD Circular 12/2024 (3.9.2-ii)",
                $"Quarterly interest capitalization is strictly prohibited for classified facility '{Id.Value}' with status '{Classification}'. Evergreening of non-performing loans is barred.");
        }

        if (RegularAccruedInterest.Amount > 0m)
        {
            OutstandingPrincipal += RegularAccruedInterest;
            RegularAccruedInterest = Money.Zero(Currency.BDT);
        }

        if (PenalInterest.Amount > 0m)
        {
            OutstandingPrincipal += PenalInterest;
            PenalInterest = Money.Zero(Currency.BDT);
        }
    }

    public void RenewFacility(
        Money newLimit,
        DateOnly newExpiryDate,
        decimal newInterestRate,
        string approvedByUserId,
        string sanctionAdviceReference)
    {
        ArgumentNullException.ThrowIfNull(newLimit, nameof(newLimit));
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedByUserId, nameof(approvedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(sanctionAdviceReference, nameof(sanctionAdviceReference));

        if (newLimit.Amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(newLimit), "Renewed limit must be positive.");

        if (newExpiryDate <= DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Renewed expiry date must be strictly in the future.", nameof(newExpiryDate));

        SanctionLimit = newLimit;
        ExpiryDate = newExpiryDate;
        RegularInterestRate = newInterestRate;
        PenalInterestRate = newInterestRate + 2.00m;

        RenewalStatus = ContinuousLoanRenewalStatus.Renewed;
        LastRenewalDate = DateOnly.FromDateTime(DateTime.UtcNow);
        RenewalCount++;
        IsDebitRestricted = false;

        // Cap drawing power to new sanction limit if limit was reduced
        if (DrawingPower.Amount > newLimit.Amount)
        {
            DrawingPower = newLimit;
        }
    }

    public void SetClassification(LoanClassificationGrade grade, int daysPastDue)
    {
        Classification = grade;
        DaysPastDue = daysPastDue;
    }
}
