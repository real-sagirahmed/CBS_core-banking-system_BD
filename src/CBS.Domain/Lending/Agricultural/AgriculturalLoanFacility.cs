using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Lending.Agricultural;

// ARCHITECTURAL INTENT: Short-Term Agricultural & Micro-Credit Loans conforming to
// Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 4.4.
// Enforces mandatory 10-Taka farmer savings account linkage, Krishi Card validation,
// Union Parishad certification, simple interest calculation without intra-year compounding,
// 3-tier repayment waterfall (Charges -> Simple Interest -> Principal), and
// statutory Non-Accrual interest freeze upon natural disaster rescheduling.
public class AgriculturalLoanFacility : AggregateRoot<AccountNumber>
{
    public CifNumber Cif { get; private set; }
    public string KrishiCardNumber { get; private set; }
    public AccountNumber FarmerSavingsAccount { get; private set; }
    public CropSeasonType CropType { get; private set; }
    public string LandPorchaNumber { get; private set; }
    public string UnionParishadCertificationReference { get; private set; }
    public string BranchCode { get; private set; }

    public Money SanctionLimit { get; private set; }
    public Money OutstandingPrincipal { get; private set; }
    public Money AccruedSimpleInterest { get; private set; }
    public Money AccruedCharges { get; private set; }

    public decimal AnnualInterestRate { get; private set; }
    public DateOnly SanctionDate { get; private set; }
    public DateOnly HarvestMaturityDate { get; private set; }
    public DateOnly? DisbursedDate { get; private set; }
    public AgriculturalLoanStatus Status { get; private set; }

    // BRPD 4.4.19-ii: Rescheduling & Non-Accrual status for disaster-affected crops
    public bool IsNonAccrual { get; private set; }
    public string? DisasterDeclarationReference { get; private set; }
    public DateOnly? RescheduledDate { get; private set; }

    #pragma warning disable CS8618
    protected AgriculturalLoanFacility() : base(null!) { }
    #pragma warning restore CS8618

    public AgriculturalLoanFacility(
        AccountNumber loanAccountNumber,
        CifNumber cif,
        string krishiCardNumber,
        AccountNumber farmerSavingsAccount,
        CropSeasonType cropType,
        string landPorchaNumber,
        string unionParishadRef,
        string branchCode,
        Money sanctionLimit,
        decimal annualInterestRate,
        DateOnly sanctionDate,
        DateOnly harvestMaturityDate) : base(loanAccountNumber)
    {
        ArgumentNullException.ThrowIfNull(loanAccountNumber, nameof(loanAccountNumber));
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));
        ArgumentNullException.ThrowIfNull(farmerSavingsAccount, nameof(farmerSavingsAccount));
        ArgumentNullException.ThrowIfNull(sanctionLimit, nameof(sanctionLimit));

        if (string.IsNullOrWhiteSpace(krishiCardNumber))
        {
            throw new InvariantViolationException(
                "KrishiCardMandatoryInvariant",
                "BRPD Circular 12/2024 (4.4.1-i)",
                "Short-term agricultural credit requires a verified Krishi Card (কৃষি কার্ড) reference.");
        }

        if (string.IsNullOrWhiteSpace(landPorchaNumber) && string.IsNullOrWhiteSpace(unionParishadRef))
        {
            throw new InvariantViolationException(
                "AgriLandSecurityInvariant",
                "BRPD Circular 12/2024 (4.4.3-i)",
                "Agricultural loans require either Land Porcha/Khatian verification or Union Parishad / witness certification.");
        }

        if (sanctionLimit.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidSanctionLimitInvariant",
                "BRPD Circular 12/2024 (4.4.2-i)",
                "Agricultural loan sanction limit must be strictly positive.");
        }

        if (harvestMaturityDate <= sanctionDate)
        {
            throw new ArgumentException("Harvest maturity date must be after sanction date.", nameof(harvestMaturityDate));
        }

        Cif = cif;
        KrishiCardNumber = krishiCardNumber.Trim().ToUpperInvariant();
        FarmerSavingsAccount = farmerSavingsAccount;
        CropType = cropType;
        LandPorchaNumber = landPorchaNumber?.Trim() ?? string.Empty;
        UnionParishadCertificationReference = unionParishadRef?.Trim() ?? string.Empty;
        BranchCode = branchCode.Trim();

        SanctionLimit = sanctionLimit;
        OutstandingPrincipal = Money.Zero(sanctionLimit.Currency);
        AccruedSimpleInterest = Money.Zero(sanctionLimit.Currency);
        AccruedCharges = Money.Zero(sanctionLimit.Currency);

        AnnualInterestRate = annualInterestRate;
        SanctionDate = sanctionDate;
        HarvestMaturityDate = harvestMaturityDate;
        Status = AgriculturalLoanStatus.Initiated;
        IsNonAccrual = false;
    }

    // 1. INVARIANT: Disbursement into Agricultural Account
    public void Disburse(DateOnly disbursementDate)
    {
        if (Status != AgriculturalLoanStatus.Initiated)
        {
            throw new InvariantViolationException(
                "AgriLoanAlreadyDisbursedInvariant",
                "BRPD Circular 12/2024 (4.4.6-ii)",
                $"Agricultural facility '{Id.Value}' is already in status '{Status}'.");
        }

        OutstandingPrincipal = SanctionLimit;
        DisbursedDate = disbursementDate;
        Status = AgriculturalLoanStatus.Disbursed;
    }

    // 2. INVARIANT: Simple Annual Interest Calculation without compounding (BRPD 4.4.4, 4.4.7)
    public Money AccrueSimpleInterest(DateOnly asOfDate)
    {
        if (Status == AgriculturalLoanStatus.Closed || OutstandingPrincipal.Amount <= 0)
            return Money.Zero(SanctionLimit.Currency);

        // BRPD 4.4.19-ii: Non-accrual treatment freezes interest recognition
        if (IsNonAccrual)
        {
            return Money.Zero(SanctionLimit.Currency);
        }

        var startDate = DisbursedDate ?? SanctionDate;
        if (asOfDate <= startDate) return Money.Zero(SanctionLimit.Currency);

        int totalDays = asOfDate.DayNumber - startDate.DayNumber;
        decimal calculatedInterest = OutstandingPrincipal.Amount * (AnnualInterestRate / 100m) * (totalDays / 365m);
        calculatedInterest = Math.Round(calculatedInterest, SanctionLimit.Currency.DecimalPlaces, MidpointRounding.AwayFromZero);

        AccruedSimpleInterest = new Money(calculatedInterest, SanctionLimit.Currency);
        return AccruedSimpleInterest;
    }

    public void AddCharge(Money chargeAmount)
    {
        ArgumentNullException.ThrowIfNull(chargeAmount, nameof(chargeAmount));
        if (chargeAmount.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(chargeAmount));
        AccruedCharges += chargeAmount;
    }

    // 3. INVARIANT: 3-Tier Repayment Waterfall (Charges -> Simple Interest -> Principal) (BRPD 4.4.8-ii)
    public (Money ChargesPaid, Money InterestPaid, Money PrincipalPaid, Money RefundExcess) Repay(Money repaymentAmount)
    {
        ArgumentNullException.ThrowIfNull(repaymentAmount, nameof(repaymentAmount));
        if (repaymentAmount.Amount <= 0) throw new ArgumentException("Repayment amount must be positive.", nameof(repaymentAmount));

        var remaining = repaymentAmount;

        // Step 1: Charges/Duties
        Money chargesPaid = Money.Zero(SanctionLimit.Currency);
        if (AccruedCharges.Amount > 0)
        {
            chargesPaid = remaining >= AccruedCharges ? AccruedCharges : remaining;
            AccruedCharges -= chargesPaid;
            remaining -= chargesPaid;
        }

        // Step 2: Simple Interest (Only if not in Non-Accrual)
        Money interestPaid = Money.Zero(SanctionLimit.Currency);
        if (remaining.Amount > 0 && AccruedSimpleInterest.Amount > 0 && !IsNonAccrual)
        {
            interestPaid = remaining >= AccruedSimpleInterest ? AccruedSimpleInterest : remaining;
            AccruedSimpleInterest -= interestPaid;
            remaining -= interestPaid;
        }

        // Step 3: Principal
        Money principalPaid = Money.Zero(SanctionLimit.Currency);
        if (remaining.Amount > 0 && OutstandingPrincipal.Amount > 0)
        {
            principalPaid = remaining >= OutstandingPrincipal ? OutstandingPrincipal : remaining;
            OutstandingPrincipal -= principalPaid;
            remaining -= principalPaid;
        }

        if (OutstandingPrincipal.Amount == 0 && AccruedSimpleInterest.Amount == 0 && AccruedCharges.Amount == 0)
        {
            Status = AgriculturalLoanStatus.Closed;
        }

        return (chargesPaid, interestPaid, principalPaid, remaining);
    }

    // 4. INVARIANT: Disaster Rescheduling & Non-Accrual Treatment (BRPD 4.4.19-ii)
    public void RescheduleForDisaster(DateOnly newMaturityDate, string disasterReference, DateOnly rescheduleDate)
    {
        if (newMaturityDate <= HarvestMaturityDate)
        {
            throw new ArgumentException("New maturity date must extend beyond current harvest maturity.", nameof(newMaturityDate));
        }

        if (string.IsNullOrWhiteSpace(disasterReference))
        {
            throw new ArgumentException("Disaster declaration reference is required.", nameof(disasterReference));
        }

        HarvestMaturityDate = newMaturityDate;
        DisasterDeclarationReference = disasterReference.Trim();
        RescheduledDate = rescheduleDate;
        IsNonAccrual = true;
        Status = AgriculturalLoanStatus.RescheduledNonAccrual;
    }
}
