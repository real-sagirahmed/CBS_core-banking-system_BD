using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Lending.Demand;

// ARCHITECTURAL INTENT: Demand Loan Lifecycle, daily product interest accrual,
// interest suspense on overdue facilities, interest capitalization, and forced PAD conversion
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.5 and 5.1.4.
public class DemandLoanFacility : AggregateRoot<AccountNumber>
{
    public CifNumber Cif { get; private set; }
    public DemandLoanType Type { get; private set; }
    public string ProductCode { get; private set; }
    public string LoanReferenceNumber { get; private set; }
    public string MisEconomicSectorCode { get; private set; }
    public string MisIndustryCode { get; private set; }
    public string BranchCode { get; private set; }

    public Money SanctionLimit { get; private set; }
    public Money OutstandingPrincipal { get; private set; }
    public Money RegularAccruedInterest { get; private set; }
    public Money OverdueInterest { get; private set; }
    public Money PenalInterest { get; private set; }
    public Money AccruedCharges { get; private set; }
    public Money InterestSuspense { get; private set; }

    public decimal AnnualInterestRate { get; private set; }
    public decimal PenalInterestRate { get; private set; }
    public DateOnly SanctionDate { get; private set; }
    public DateOnly MaturityDate { get; private set; }
    public DateOnly? DisbursedDate { get; private set; }

    public DemandLoanStatus Status { get; private set; }
    public RepaymentPriorityScheme RepaymentPriority { get; private set; }
    public DateOnly? CapitalizedDate { get; private set; }
    public string? CapitalizationApprovalReference { get; private set; }

    #pragma warning disable CS8618
    protected DemandLoanFacility() : base(null!) { }
    #pragma warning restore CS8618

    public DemandLoanFacility(
        AccountNumber loanAccountNumber,
        CifNumber cif,
        DemandLoanType type,
        string productCode,
        string loanReferenceNumber,
        string misEconomicSectorCode,
        string misIndustryCode,
        string branchCode,
        Money sanctionLimit,
        decimal annualInterestRate,
        decimal penalInterestRate,
        DateOnly sanctionDate,
        DateOnly maturityDate,
        RepaymentPriorityScheme repaymentPriority = RepaymentPriorityScheme.Standard) : base(loanAccountNumber)
    {
        ArgumentNullException.ThrowIfNull(loanAccountNumber, nameof(loanAccountNumber));
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));
        ArgumentNullException.ThrowIfNull(sanctionLimit, nameof(sanctionLimit));
        if (string.IsNullOrWhiteSpace(productCode)) throw new ArgumentException("Product code is required.", nameof(productCode));
        if (string.IsNullOrWhiteSpace(loanReferenceNumber)) throw new ArgumentException("Loan reference number is required.", nameof(loanReferenceNumber));
        if (string.IsNullOrWhiteSpace(misEconomicSectorCode)) throw new ArgumentException("MIS economic sector code is required.", nameof(misEconomicSectorCode));
        if (string.IsNullOrWhiteSpace(misIndustryCode)) throw new ArgumentException("MIS industry code is required.", nameof(misIndustryCode));
        if (string.IsNullOrWhiteSpace(branchCode)) throw new ArgumentException("Branch code is required.", nameof(branchCode));

        if (sanctionLimit.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidDemandLoanSanctionLimitInvariant",
                "BRPD Circular 12/2024 (4.5.1)",
                $"Demand loan sanction limit must be strictly positive (provided: {sanctionLimit.Amount}).");
        }

        if (maturityDate <= sanctionDate)
        {
            throw new InvariantViolationException(
                "InvalidMaturityDateInvariant",
                "BRPD Circular 12/2024 (4.5.1)",
                $"Maturity date ({maturityDate}) must be strictly after sanction date ({sanctionDate}).");
        }

        Cif = cif;
        Type = type;
        ProductCode = productCode.Trim();
        LoanReferenceNumber = loanReferenceNumber.Trim();
        MisEconomicSectorCode = misEconomicSectorCode.Trim();
        MisIndustryCode = misIndustryCode.Trim();
        BranchCode = branchCode.Trim();
        SanctionLimit = sanctionLimit;
        OutstandingPrincipal = Money.Zero(Currency.BDT);
        RegularAccruedInterest = Money.Zero(Currency.BDT);
        OverdueInterest = Money.Zero(Currency.BDT);
        PenalInterest = Money.Zero(Currency.BDT);
        AccruedCharges = Money.Zero(Currency.BDT);
        InterestSuspense = Money.Zero(Currency.BDT);
        AnnualInterestRate = annualInterestRate;
        PenalInterestRate = penalInterestRate;
        SanctionDate = sanctionDate;
        MaturityDate = maturityDate;
        Status = DemandLoanStatus.Sanctioned;
        RepaymentPriority = repaymentPriority;
    }

    /// <summary>
    /// Static factory to auto-generate a forced PAD demand loan upon import bill sight maturity default (BRPD 5.1.4, 4.5).
    /// </summary>
    public static DemandLoanFacility CreateForcedPadFromImportBill(
        AccountNumber loanAccountNumber,
        CifNumber cif,
        string branchCode,
        string importLcNumber,
        string billDocumentNumber,
        Money billAmount,
        decimal annualInterestRate,
        decimal penalInterestRate,
        DateOnly conversionDate,
        DateOnly maturityDate,
        string misSector = "SEC-TRADE-IMP",
        string misIndustry = "IND-COMMERCIAL")
    {
        if (string.IsNullOrWhiteSpace(importLcNumber)) throw new ArgumentException("Import LC number is required.", nameof(importLcNumber));
        if (string.IsNullOrWhiteSpace(billDocumentNumber)) throw new ArgumentException("Bill document number is required.", nameof(billDocumentNumber));

        string loanRef = $"PAD-{importLcNumber.Trim()}-{billDocumentNumber.Trim()}";
        var facility = new DemandLoanFacility(
            loanAccountNumber,
            cif,
            DemandLoanType.PaymentAgainstDocuments_PAD,
            "451", // BRPD 4.5.1 unique product code for PAD
            loanRef,
            misSector,
            misIndustry,
            branchCode,
            billAmount,
            annualInterestRate,
            penalInterestRate,
            conversionDate,
            maturityDate);

        // Immediate disbursement to retire trade documents
        facility.Disburse(billAmount, conversionDate, $"Retirement of Import LC Bill {billDocumentNumber}");
        return facility;
    }

    public void Disburse(Money amount, DateOnly disbursementDate, string? remarks = null)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));

        if (Status != DemandLoanStatus.Sanctioned && Status != DemandLoanStatus.Disbursed)
        {
            throw new InvariantViolationException(
                "InvalidDemandLoanStatusForDisbursementInvariant",
                "BRPD Circular 12/2024 (4.5.1)",
                $"Cannot disburse demand loan '{Id.Value}' with status '{Status}'.");
        }

        if (amount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidDisbursementAmountInvariant",
                "BRPD Circular 12/2024 (4.5.1)",
                "Disbursement amount must be strictly positive.");
        }

        if (OutstandingPrincipal + amount > SanctionLimit)
        {
            throw new InvariantViolationException(
                "DisbursementExceedsSanctionLimitInvariant",
                "BRPD Circular 12/2024 (4.5.1)",
                $"Disbursement of {amount} would exceed sanctioned limit ({SanctionLimit}).");
        }

        OutstandingPrincipal += amount;
        DisbursedDate = disbursementDate;
        Status = DemandLoanStatus.Disbursed;
    }

    /// <summary>
    /// Accrues daily interest on the daily product basis (BRPD 4.5.2-i).
    /// If loan is overdue or classified, applies interest suspense treatment (BRPD 4.5.2-ii).
    /// </summary>
    public void AccrueDailyInterest(DateOnly accrualDate, bool isOverdueOrClassified = false)
    {
        if (Status == DemandLoanStatus.Liquidated || OutstandingPrincipal.Amount <= 0)
            return;

        // Daily product calculation: (OutstandingPrincipal * AnnualRate) / (365 * 100)
        decimal dailyInterestAmount = Math.Round(OutstandingPrincipal.Amount * (AnnualInterestRate / 100m) / 365m, 2, MidpointRounding.AwayFromZero);
        var dailyInterest = Money.Bdt(dailyInterestAmount);

        // Check if overdue by date or explicitly flagged
        bool overdue = isOverdueOrClassified || accrualDate > MaturityDate || Status == DemandLoanStatus.Overdue;

        if (overdue)
        {
            Status = DemandLoanStatus.Overdue;
            // BRPD 4.5.2-ii: Divert interest to Interest Suspense (stops P&L recognition)
            InterestSuspense += dailyInterest;
            OverdueInterest += dailyInterest;

            // Apply penal interest for overdue demand loans
            if (PenalInterestRate > 0)
            {
                decimal dailyPenalAmt = Math.Round(OutstandingPrincipal.Amount * (PenalInterestRate / 100m) / 365m, 2, MidpointRounding.AwayFromZero);
                PenalInterest += Money.Bdt(dailyPenalAmt);
            }
        }
        else
        {
            RegularAccruedInterest += dailyInterest;
        }
    }

    /// <summary>
    /// Capitalizes accrued and overdue interest into the principal balance as per central bank restructuring terms (BRPD 4.5.2-iii).
    /// </summary>
    public void CapitalizeInterest(string approvalReference, DateOnly capitalizationDate)
    {
        if (string.IsNullOrWhiteSpace(approvalReference))
        {
            throw new ArgumentException("Formal restructuring/capitalization approval reference is required.", nameof(approvalReference));
        }

        if (Status == DemandLoanStatus.Liquidated)
        {
            throw new InvariantViolationException(
                "CannotCapitalizeLiquidatedLoanInvariant",
                "BRPD Circular 12/2024 (4.5.2-iii)",
                $"Demand loan '{Id.Value}' is already liquidated.");
        }

        Money totalInterestToCapitalize = RegularAccruedInterest + OverdueInterest + PenalInterest;
        if (totalInterestToCapitalize.Amount <= 0)
        {
            throw new InvariantViolationException(
                "ZeroInterestToCapitalizeInvariant",
                "BRPD Circular 12/2024 (4.5.2-iii)",
                "No accrued or overdue interest available to capitalize.");
        }

        // Capitalize into principal schedule
        OutstandingPrincipal += totalInterestToCapitalize;

        // Clear out capitalized interest balances & suspense
        RegularAccruedInterest = Money.Zero(Currency.BDT);
        OverdueInterest = Money.Zero(Currency.BDT);
        PenalInterest = Money.Zero(Currency.BDT);
        InterestSuspense = Money.Zero(Currency.BDT);

        CapitalizedDate = capitalizationDate;
        CapitalizationApprovalReference = approvalReference.Trim();
        Status = DemandLoanStatus.Restructured;
    }

    /// <summary>
    /// Processes partial or full loan repayment with product-wise recovery priorities (BRPD 4.5.4-ii, iii).
    /// </summary>
    public void Repay(Money amount, RepaymentSourceType source, DateOnly paymentDate)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));

        if (amount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidRepaymentAmountInvariant",
                "BRPD Circular 12/2024 (4.5.4)",
                "Repayment amount must be strictly positive.");
        }

        decimal remaining = amount.Amount;

        if (RepaymentPriority == RepaymentPriorityScheme.Standard)
        {
            // Standard Priority: Charges -> Penal Interest -> Overdue Interest -> Regular Interest -> Principal
            remaining = RecoverBucket(remaining, AccruedCharges, val => AccruedCharges = val);
            remaining = RecoverBucket(remaining, PenalInterest, val => PenalInterest = val);
            remaining = RecoverBucket(remaining, OverdueInterest, val =>
            {
                // When overdue interest is recovered, decrease Interest Suspense correspondingly
                decimal recovered = OverdueInterest.Amount - val.Amount;
                OverdueInterest = val;
                if (InterestSuspense.Amount >= recovered)
                    InterestSuspense -= Money.Bdt(recovered);
                else
                    InterestSuspense = Money.Zero(Currency.BDT);
            });
            remaining = RecoverBucket(remaining, RegularAccruedInterest, val => RegularAccruedInterest = val);
            remaining = RecoverBucket(remaining, OutstandingPrincipal, val => OutstandingPrincipal = val);
        }
        else // PrincipalFirst
        {
            // Restructured Priority: Penal Interest -> Overdue Interest -> Principal -> Regular Interest -> Charges
            remaining = RecoverBucket(remaining, PenalInterest, val => PenalInterest = val);
            remaining = RecoverBucket(remaining, OverdueInterest, val =>
            {
                decimal recovered = OverdueInterest.Amount - val.Amount;
                OverdueInterest = val;
                if (InterestSuspense.Amount >= recovered)
                    InterestSuspense -= Money.Bdt(recovered);
                else
                    InterestSuspense = Money.Zero(Currency.BDT);
            });
            remaining = RecoverBucket(remaining, OutstandingPrincipal, val => OutstandingPrincipal = val);
            remaining = RecoverBucket(remaining, RegularAccruedInterest, val => RegularAccruedInterest = val);
            remaining = RecoverBucket(remaining, AccruedCharges, val => AccruedCharges = val);
        }

        // Check if fully liquidated
        if (OutstandingPrincipal.Amount == 0 &&
            RegularAccruedInterest.Amount == 0 &&
            OverdueInterest.Amount == 0 &&
            PenalInterest.Amount == 0 &&
            AccruedCharges.Amount == 0)
        {
            Status = DemandLoanStatus.Liquidated;
        }
        else if (Status == DemandLoanStatus.Overdue && paymentDate <= MaturityDate && OverdueInterest.Amount == 0)
        {
            Status = DemandLoanStatus.Disbursed;
        }
    }

    public void MarkOverdue(DateOnly currentDate)
    {
        if (Status == DemandLoanStatus.Disbursed && currentDate > MaturityDate)
        {
            Status = DemandLoanStatus.Overdue;
        }
    }

    private static decimal RecoverBucket(decimal remainingPayment, Money currentBucket, Action<Money> updateBucket)
    {
        if (remainingPayment <= 0 || currentBucket.Amount <= 0)
            return remainingPayment;

        if (remainingPayment >= currentBucket.Amount)
        {
            remainingPayment -= currentBucket.Amount;
            updateBucket(Money.Zero(Currency.BDT));
        }
        else
        {
            updateBucket(Money.Bdt(currentBucket.Amount - remainingPayment));
            remainingPayment = 0m;
        }

        return remainingPayment;
    }
}
