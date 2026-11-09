using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Lending;

// ARCHITECTURAL INTENT: Loan Facility Lifecycle, CAD duty segregation, statutory recovery waterfall,
// Islamic Shariah vendor payment locks, and BB Asset Classification (CL) interest suspense (BRPD Circular 12/2024 Module 4).
public class LoanAccount : AggregateRoot<AccountNumber>
{
    public CifNumber Cif { get; private set; }
    public LoanFacilityType FacilityType { get; private set; }
    public string BranchCode { get; private set; }
    public Money SanctionLimit { get; private set; }
    public Money DrawingPower { get; private set; }
    public Money OutstandingPrincipal { get; private set; }
    public Money RegularAccruedInterest { get; private set; }
    public Money OverdueInterest { get; private set; }
    public Money PenalInterest { get; private set; }
    public Money AccruedCharges { get; private set; }
    public Money InterestSuspense { get; private set; }
    public decimal InterestRate { get; private set; }
    public bool IsCadVetted { get; private set; }
    public string? CadVettedByUserId { get; private set; }
    public DateTime? CadVettedAtUtc { get; private set; }
    public DateOnly SanctionDate { get; private set; }
    public DateOnly MaturityDate { get; private set; }
    public DateOnly? DisbursedDate { get; private set; }
    public string? DisbursedByUserId { get; private set; }
    public string? DisbursementBeneficiarySupplier { get; private set; }
    public LoanClassificationGrade Classification { get; private set; }
    public int DaysPastDue { get; private set; }
    public bool IsShariahCompliant { get; private set; }

    public LoanAccount(
        AccountNumber accountNumber,
        CifNumber cif,
        LoanFacilityType facilityType,
        string branchCode,
        Money sanctionLimit,
        Money drawingPower,
        decimal interestRate,
        DateOnly sanctionDate,
        DateOnly maturityDate) : base(accountNumber)
    {
        ArgumentNullException.ThrowIfNull(accountNumber, nameof(accountNumber));
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));
        ArgumentNullException.ThrowIfNull(sanctionLimit, nameof(sanctionLimit));
        ArgumentNullException.ThrowIfNull(drawingPower, nameof(drawingPower));
        if (string.IsNullOrWhiteSpace(branchCode)) throw new ArgumentException("Branch code is required.", nameof(branchCode));

        if (sanctionLimit.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidSanctionLimitInvariant",
                "BRPD Circular 12/2024 (4.1.3)",
                "Loan facility sanction limit must be strictly positive.");
        }

        if (drawingPower > sanctionLimit)
        {
            throw new InvariantViolationException(
                "DrawingPowerExceedsSanctionLimitInvariant",
                "BRPD Circular 12/2024 (4.1.6)",
                $"Drawing Power ({drawingPower}) cannot exceed sanctioned facility limit ({sanctionLimit}).");
        }

        // Quard contract invariant: Strict 0.00% interest (BRPD 4.3.4-i)
        if (facilityType == LoanFacilityType.Islamic_Quard && interestRate != 0.00m)
        {
            throw new InvariantViolationException(
                "QuardNonZeroInterestInvariant",
                "BRPD Circular 12/2024 (4.3.4-i)",
                $"Islamic Quard-e-Hasana contracts must carry strictly 0.00% interest/profit (provided: {interestRate}%).");
        }

        Cif = cif;
        FacilityType = facilityType;
        BranchCode = branchCode.Trim();
        SanctionLimit = sanctionLimit;
        DrawingPower = drawingPower;
        OutstandingPrincipal = Money.Zero(Currency.BDT);
        RegularAccruedInterest = Money.Zero(Currency.BDT);
        OverdueInterest = Money.Zero(Currency.BDT);
        PenalInterest = Money.Zero(Currency.BDT);
        AccruedCharges = Money.Zero(Currency.BDT);
        InterestSuspense = Money.Zero(Currency.BDT);
        InterestRate = interestRate;
        SanctionDate = sanctionDate;
        MaturityDate = maturityDate;
        Classification = LoanClassificationGrade.Standard;
        DaysPastDue = 0;
        IsCadVetted = false;
        IsShariahCompliant = facilityType is LoanFacilityType.Islamic_BaiMurabaha or LoanFacilityType.Islamic_Quard;
    }

    public void VetCadChecklist(string cadOfficerUserId)
    {
        if (string.IsNullOrWhiteSpace(cadOfficerUserId))
            throw new ArgumentException("CAD officer User ID is required.", nameof(cadOfficerUserId));

        IsCadVetted = true;
        CadVettedByUserId = cadOfficerUserId.Trim();
        CadVettedAtUtc = DateTime.UtcNow;
    }

    public void SetCharges(Money charges)
    {
        ArgumentNullException.ThrowIfNull(charges, nameof(charges));
        AccruedCharges += charges;
    }

    public void SetPenalInterest(Money penal)
    {
        ArgumentNullException.ThrowIfNull(penal, nameof(penal));
        PenalInterest += penal;
    }

    public void Disburse(
        Money amount,
        string branchDisbursingUserId,
        string? supplierPayOrderBeneficiary = null)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        if (string.IsNullOrWhiteSpace(branchDisbursingUserId))
            throw new ArgumentException("Disbursing officer User ID is required.", nameof(branchDisbursingUserId));

        if (amount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidDisbursementAmountInvariant",
                "BRPD Circular 12/2024 (4.1.5)",
                "Disbursement amount must be strictly positive.");
        }

        // 1. HARD GATE: CAD Checklist Vetting Verification (BRPD 4.1.3-i, 4.1.5-i)
        if (!IsCadVetted)
        {
            throw new InvariantViolationException(
                "CadChecklistNotVettedInvariant",
                "BRPD Circular 12/2024 (4.1.3-i, 4.1.5-i)",
                $"Disbursement for facility '{Id.Value}' is prohibited. CAD legal checklist and charge documents must be vetted and signed off before release of funds.");
        }

        // 2. HARD GATE: Maker-Checker / Disbursing vs CAD Officer segregation (BRPD 1.3.18, 10.3.5-x)
        if (branchDisbursingUserId.Equals(CadVettedByUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"CAD vetting officer '{CadVettedByUserId}' cannot disburse funds for facility '{Id.Value}'. Mandatory 4-eyes duty segregation.");
        }

        // 3. HARD GATE: Drawing Power limit check (BRPD 4.1.6)
        if ((OutstandingPrincipal + amount) > DrawingPower)
        {
            throw new InvariantViolationException(
                "DrawingPowerExceededInvariant",
                "BRPD Circular 12/2024 (4.1.6)",
                $"Disbursement of {amount} would cause outstanding balance ({OutstandingPrincipal + amount}) to breach Drawing Power ({DrawingPower}).");
        }

        // 4. HARD GATE: Islamic Bai-Murabaha direct vendor disbursement lock (BRPD 4.1.5-iv)
        if (FacilityType == LoanFacilityType.Islamic_BaiMurabaha)
        {
            if (string.IsNullOrWhiteSpace(supplierPayOrderBeneficiary))
            {
                throw new InvariantViolationException(
                    "MurabahaDirectVendorDisbursementInvariant",
                    "BRPD Circular 12/2024 (4.1.5-iv)",
                    "Bai-Murabaha disbursement requires verified goods supplier/vendor beneficiary for direct Pay Order / RTGS remittance. Direct cash disbursement or credit to customer general account is strictly prohibited.");
            }

            DisbursementBeneficiarySupplier = supplierPayOrderBeneficiary.Trim();
        }

        OutstandingPrincipal += amount;
        DisbursedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        DisbursedByUserId = branchDisbursingUserId.Trim();
    }

    public void AccrueDailyInterest(int days = 1)
    {
        if (OutstandingPrincipal.Amount <= 0 || InterestRate <= 0) return;

        decimal dailyRate = InterestRate / (100m * 365m);
        decimal accruedAmount = Math.Round(OutstandingPrincipal.Amount * dailyRate * days, 2, MidpointRounding.AwayFromZero);
        var accruedMoney = Money.Bdt(accruedAmount);

        // HARD GATE: Interest Suspension for Classified Accounts (BRPD 3.9.2-i, 4.1.11-iii)
        // SS, DF, BL accounts MUST suspend interest accrual (route to Suspense, NEVER to Bank P&L Income)
        if (Classification is LoanClassificationGrade.SubStandard_SS or LoanClassificationGrade.Doubtful_DF or LoanClassificationGrade.BadLoss_BL)
        {
            InterestSuspense += accruedMoney;
        }
        else
        {
            RegularAccruedInterest += accruedMoney;
        }
    }

    public RepaymentBreakdown ProcessRepaymentWaterfall(Money paymentAmount)
    {
        ArgumentNullException.ThrowIfNull(paymentAmount, nameof(paymentAmount));
        if (paymentAmount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidRepaymentAmountInvariant",
                "BRPD Circular 12/2024 (4.2.11-iii)",
                "Repayment amount must be strictly positive.");
        }

        decimal unallocated = paymentAmount.Amount;

        // 1. Recover Charges
        decimal allocCharges = Math.Min(unallocated, AccruedCharges.Amount);
        AccruedCharges -= Money.Bdt(allocCharges);
        unallocated -= allocCharges;

        // 2. Recover Penal Interest
        decimal allocPenal = Math.Min(unallocated, PenalInterest.Amount);
        PenalInterest -= Money.Bdt(allocPenal);
        unallocated -= allocPenal;

        // 3. Recover Overdue Interest
        decimal allocOverdue = Math.Min(unallocated, OverdueInterest.Amount);
        OverdueInterest -= Money.Bdt(allocOverdue);
        unallocated -= allocOverdue;

        // 4. Recover Regular Accrued Interest
        decimal allocRegular = Math.Min(unallocated, RegularAccruedInterest.Amount);
        RegularAccruedInterest -= Money.Bdt(allocRegular);
        unallocated -= allocRegular;

        // 5. Recover Outstanding Principal
        decimal allocPrincipal = Math.Min(unallocated, OutstandingPrincipal.Amount);
        OutstandingPrincipal -= Money.Bdt(allocPrincipal);
        unallocated -= allocPrincipal;

        return new RepaymentBreakdown(
            Money.Bdt(allocCharges),
            Money.Bdt(allocPenal),
            Money.Bdt(allocOverdue),
            Money.Bdt(allocRegular),
            Money.Bdt(allocPrincipal),
            paymentAmount,
            Money.Bdt(unallocated));
    }

    public void EvaluateClassification(int daysPastDue)
    {
        DaysPastDue = Math.Max(0, daysPastDue);
        var previousGrade = Classification;

        Classification = DaysPastDue switch
        {
            < 60 => LoanClassificationGrade.Standard,
            < 180 => LoanClassificationGrade.SpecialMentionAccount_SMA,
            < 270 => LoanClassificationGrade.SubStandard_SS,
            < 360 => LoanClassificationGrade.Doubtful_DF,
            _ => LoanClassificationGrade.BadLoss_BL
        };

        // When account becomes classified (SS or worse), transfer uncollected regular interest to Interest Suspense (BRPD 4.1.11-iii)
        if (Classification >= LoanClassificationGrade.SubStandard_SS && previousGrade < LoanClassificationGrade.SubStandard_SS)
        {
            InterestSuspense += RegularAccruedInterest;
            RegularAccruedInterest = Money.Zero(Currency.BDT);
        }
    }
}
