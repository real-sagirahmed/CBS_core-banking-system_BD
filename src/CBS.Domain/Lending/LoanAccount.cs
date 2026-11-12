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
    public string? SanctionApprovedByUserId { get; private set; }
    public string? DisbursementBeneficiarySupplier { get; private set; }
    public LoanClassificationGrade Classification { get; private set; }
    public int DaysPastDue { get; private set; }
    public bool IsShariahCompliant { get; private set; }
    public Money EligibleCollateralFsv { get; private set; }
    public Money RequiredGeneralProvision { get; private set; }
    public Money RequiredSpecificProvision { get; private set; }
    public int RescheduleCount { get; private set; }
    public bool IsWrittenOff { get; private set; }
    public LoanAccountStatus LoanStatus { get; private set; }
    public bool HasQualitativeJudgment { get; private set; }
    public LoanClassificationGrade? QualitativeJudgmentGrade { get; private set; }
    public string? QualitativeJudgmentReason { get; private set; }
    public string? RescheduledFromLoanAccountNumber { get; private set; }

    #pragma warning disable CS8618
    protected LoanAccount() : base(null!) { }
    #pragma warning restore CS8618

    public LoanAccount(
        AccountNumber accountNumber,
        CifNumber cif,
        LoanFacilityType facilityType,
        string branchCode,
        Money sanctionLimit,
        Money drawingPower,
        decimal interestRate,
        DateOnly sanctionDate,
        DateOnly maturityDate,
        string? sanctionApprovedByUserId = null,
        string? rescheduledFromLoanAccountNumber = null) : base(accountNumber)
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
        EligibleCollateralFsv = Money.Zero(Currency.BDT);
        RequiredGeneralProvision = Money.Zero(Currency.BDT);
        RequiredSpecificProvision = Money.Zero(Currency.BDT);
        InterestRate = interestRate;
        SanctionDate = sanctionDate;
        MaturityDate = maturityDate;
        Classification = LoanClassificationGrade.Standard;
        DaysPastDue = 0;
        RescheduleCount = 0;
        IsWrittenOff = false;
        LoanStatus = LoanAccountStatus.Active;
        HasQualitativeJudgment = false;
        QualitativeJudgmentGrade = null;
        QualitativeJudgmentReason = null;
        SanctionApprovedByUserId = sanctionApprovedByUserId?.Trim();
        RescheduledFromLoanAccountNumber = rescheduledFromLoanAccountNumber?.Trim();
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

        // 2. HARD GATE: Maker-Checker / Disbursing vs CAD Officer & Approver segregation (BRPD 1.3.18, 10.3.5-x)
        if (branchDisbursingUserId.Equals(CadVettedByUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"CAD vetting officer '{CadVettedByUserId}' cannot disburse funds for facility '{Id.Value}'. Mandatory 4-eyes duty segregation.");
        }

        if (!string.IsNullOrWhiteSpace(SanctionApprovedByUserId) && branchDisbursingUserId.Equals(SanctionApprovedByUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Sanctioning approver '{SanctionApprovedByUserId}' cannot disburse funds for facility '{Id.Value}'. Mandatory 3-tier duty segregation.");
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

        var objectiveGrade = DaysPastDue switch
        {
            < 60 => LoanClassificationGrade.Standard,
            < 180 => LoanClassificationGrade.SpecialMentionAccount_SMA,
            < 270 => LoanClassificationGrade.SubStandard_SS,
            < 360 => LoanClassificationGrade.Doubtful_DF,
            _ => LoanClassificationGrade.BadLoss_BL
        };

        // Qualitative Judgment (QJ) Override Lock (BRPD 4.1.11-iii, 4.2.27-iii, Rule 5.2):
        // An account downgraded via QJ cannot be auto-upgraded by EOD DPD aging alone without explicit committee approval.
        if (HasQualitativeJudgment && QualitativeJudgmentGrade.HasValue)
        {
            Classification = (LoanClassificationGrade)Math.Max((int)objectiveGrade, (int)QualitativeJudgmentGrade.Value);
        }
        else
        {
            Classification = objectiveGrade;
        }

        // When account becomes classified (SS or worse), transfer uncollected regular interest to Interest Suspense (BRPD 4.1.11-iii)
        if (Classification >= LoanClassificationGrade.SubStandard_SS && previousGrade < LoanClassificationGrade.SubStandard_SS)
        {
            InterestSuspense += RegularAccruedInterest;
            RegularAccruedInterest = Money.Zero(Currency.BDT);
        }
    }

    public void SetEligibleCollateralFsv(Money collateralFsv)
    {
        ArgumentNullException.ThrowIfNull(collateralFsv, nameof(collateralFsv));
        EligibleCollateralFsv = collateralFsv;
    }

    public Money CalculateProvisionBase()
    {
        // Base for Provision = Max(0, OutstandingPrincipal - EligibleCollateralFsv - InterestSuspense)
        decimal baseAmount = Math.Max(0m, OutstandingPrincipal.Amount - EligibleCollateralFsv.Amount - InterestSuspense.Amount);
        return Money.Bdt(baseAmount);
    }

    public void CalculateProvisionRequirement(decimal generalProvisionRate, decimal specificProvisionRate)
    {
        var provisionBase = CalculateProvisionBase();

        if (Classification is LoanClassificationGrade.Standard or LoanClassificationGrade.SpecialMentionAccount_SMA)
        {
            // General provision is calculated on total outstanding
            decimal genProv = Math.Round(OutstandingPrincipal.Amount * (generalProvisionRate / 100m), 2, MidpointRounding.AwayFromZero);
            RequiredGeneralProvision = Money.Bdt(genProv);
            RequiredSpecificProvision = Money.Zero(Currency.BDT);
        }
        else
        {
            // Specific provision is calculated on Base for Provision (net of eligible collateral & suspense)
            decimal specProv = Math.Round(provisionBase.Amount * (specificProvisionRate / 100m), 2, MidpointRounding.AwayFromZero);
            RequiredGeneralProvision = Money.Zero(Currency.BDT);
            RequiredSpecificProvision = Money.Bdt(specProv);
        }
    }

    public void ApplyQualitativeJudgment(
        LoanClassificationGrade grade,
        QualitativeJudgmentReason reason,
        string justification,
        string inspectorId,
        string checkerId)
    {
        if (string.IsNullOrWhiteSpace(inspectorId))
            throw new ArgumentException("Inspector user ID is required.", nameof(inspectorId));
        if (string.IsNullOrWhiteSpace(checkerId))
            throw new ArgumentException("Checker user ID is required.", nameof(checkerId));
        if (string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("Qualitative judgment justification is required.", nameof(justification));

        if (string.Equals(inspectorId.Trim(), checkerId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                "Inspector cannot act as Checker to authorize Qualitative Judgment override.");
        }

        if (grade < LoanClassificationGrade.SubStandard_SS)
        {
            throw new InvariantViolationException(
                "QualitativeJudgmentDowngradeOnlyInvariant",
                "BRPD Circular 12/2024 (4.1.11-iii, 4.2.27-iii)",
                "Qualitative Judgment (QJ) can only be applied for non-performing classification (SS, DF, BL).");
        }

        if (grade < Classification)
        {
            throw new InvariantViolationException(
                "QualitativeJudgmentCannotUpgradeInvariant",
                "BRPD Circular 12/2024 (4.2.27-iii, Rule 5.2)",
                $"Qualitative Judgment cannot upgrade an account from '{Classification}' to '{grade}'. QJ is strictly for adverse classification.");
        }

        var previousGrade = Classification;
        HasQualitativeJudgment = true;
        QualitativeJudgmentGrade = grade;
        QualitativeJudgmentReason = reason.ToString();
        Classification = grade;

        if (Classification >= LoanClassificationGrade.SubStandard_SS && previousGrade < LoanClassificationGrade.SubStandard_SS)
        {
            InterestSuspense += RegularAccruedInterest;
            RegularAccruedInterest = Money.Zero(Currency.BDT);
        }
    }

    public void RevokeQualitativeJudgment(string justification, string inspectorId, string checkerId)
    {
        if (string.IsNullOrWhiteSpace(inspectorId))
            throw new ArgumentException("Inspector user ID is required.", nameof(inspectorId));
        if (string.IsNullOrWhiteSpace(checkerId))
            throw new ArgumentException("Checker user ID is required.", nameof(checkerId));
        if (string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("Revocation justification is required.", nameof(justification));

        if (string.Equals(inspectorId.Trim(), checkerId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                "Inspector cannot act as Checker to revoke Qualitative Judgment override.");
        }

        if (!HasQualitativeJudgment)
        {
            throw new InvalidOperationException("Loan account does not have an active Qualitative Judgment override.");
        }

        HasQualitativeJudgment = false;
        QualitativeJudgmentGrade = null;
        QualitativeJudgmentReason = null;

        // Re-evaluate objective classification based on actual DaysPastDue
        EvaluateClassification(DaysPastDue);
    }

    public void Reschedule(
        int newRescheduleIteration,
        Money downPaymentPaid,
        Money requiredDownPayment,
        DateOnly newMaturityDate,
        decimal newInterestRate,
        string boardResolutionRef,
        DateOnly boardApprovalDate,
        Money? interestWaiver,
        string makerUserId,
        string checkerUserId)
    {
        ArgumentNullException.ThrowIfNull(downPaymentPaid, nameof(downPaymentPaid));
        ArgumentNullException.ThrowIfNull(requiredDownPayment, nameof(requiredDownPayment));
        if (string.IsNullOrWhiteSpace(makerUserId)) throw new ArgumentException("Maker user ID is required.", nameof(makerUserId));
        if (string.IsNullOrWhiteSpace(checkerUserId)) throw new ArgumentException("Checker user ID is required.", nameof(checkerUserId));

        if (string.Equals(makerUserId.Trim(), checkerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                "Maker cannot act as Checker to approve loan rescheduling.");
        }

        if (newRescheduleIteration > 3)
        {
            throw new InvariantViolationException(
                "RescheduleMaxLimitExceededInvariant",
                "BRPD Circular 16/2020 & 04/2023 (Rule 5.3)",
                $"Loan facility '{Id.Value}' has reached the maximum permitted 3 reschedulings. 4th rescheduling is strictly prohibited without explicit Bangladesh Bank NOC.");
        }

        if (downPaymentPaid < requiredDownPayment)
        {
            throw new InvariantViolationException(
                "RescheduleDownPaymentInsufficientInvariant",
                "BRPD Circular 16/2020 & 04/2023",
                $"Down payment of {downPaymentPaid} BDT is less than statutory mandatory down payment of {requiredDownPayment} BDT.");
        }

        if (newRescheduleIteration >= 2 && string.IsNullOrWhiteSpace(boardResolutionRef))
        {
            throw new InvariantViolationException(
                "RescheduleBoardApprovalMandatoryInvariant",
                "BRPD Circular 16/2020 (Rule 5.3)",
                $"Rescheduling iteration #{newRescheduleIteration} requires mandatory Board of Directors approval resolution reference.");
        }

        if (newMaturityDate <= MaturityDate)
        {
            throw new InvariantViolationException(
                "RescheduleNewMaturityDateInvariant",
                "BRPD Circular 12/2024 (4.1.12)",
                "New maturity date must extend beyond the existing maturity date.");
        }

        // Apply interest suspense de-accrual treatment (BRPD 4.1.12-ii, 4.2.28-ii)
        InterestSuspense += RegularAccruedInterest;
        RegularAccruedInterest = Money.Zero(Currency.BDT);

        // Deduct interest waiver if approved by Board
        if (interestWaiver is not null && interestWaiver.Amount > 0)
        {
            decimal waiverAmount = Math.Min(interestWaiver.Amount, InterestSuspense.Amount);
            InterestSuspense -= Money.Bdt(waiverAmount);
        }

        // Deduct down payment from outstanding principal
        decimal principalReduction = Math.Min(downPaymentPaid.Amount, OutstandingPrincipal.Amount);
        OutstandingPrincipal -= Money.Bdt(principalReduction);

        // Update terms
        RescheduleCount = newRescheduleIteration;
        MaturityDate = newMaturityDate;
        InterestRate = newInterestRate;
        LoanStatus = LoanAccountStatus.Rescheduled;

        // Reset DPD and unclassified grade upon successful formal rescheduling
        DaysPastDue = 0;
        Classification = LoanClassificationGrade.Standard;
        HasQualitativeJudgment = false;
        QualitativeJudgmentGrade = null;
        QualitativeJudgmentReason = null;
    }

    public void WriteOff(
        Money specificProvisionAvailable,
        string? arthaRinCaseNumber,
        DateOnly? arthaRinFilingDate,
        string boardResolutionNumber,
        DateOnly boardApprovalDate,
        string makerUserId,
        string checkerUserId)
    {
        ArgumentNullException.ThrowIfNull(specificProvisionAvailable, nameof(specificProvisionAvailable));
        if (string.IsNullOrWhiteSpace(makerUserId)) throw new ArgumentException("Maker user ID is required.", nameof(makerUserId));
        if (string.IsNullOrWhiteSpace(checkerUserId)) throw new ArgumentException("Checker user ID is required.", nameof(checkerUserId));
        if (string.IsNullOrWhiteSpace(boardResolutionNumber)) throw new ArgumentException("Board resolution number is required.", nameof(boardResolutionNumber));

        if (string.Equals(makerUserId.Trim(), checkerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                "Maker cannot act as Checker to approve loan write-off.");
        }

        if (Classification != LoanClassificationGrade.BadLoss_BL)
        {
            throw new InvariantViolationException(
                "WriteOffNotBadLossInvariant",
                "BRPD Circular 01/2013 & 08/2023",
                $"Cannot write off loan facility '{Id.Value}' in status '{Classification}'. Facility must be classified as Bad/Loss (BL).");
        }

        if (specificProvisionAvailable.Amount < OutstandingPrincipal.Amount)
        {
            throw new InvariantViolationException(
                "WriteOffProvisionInsufficientInvariant",
                "BRPD Circular 01/2013 & 08/2023",
                $"Cannot write off loan facility '{Id.Value}'. 100% specific provision required ({OutstandingPrincipal} BDT), but only {specificProvisionAvailable} BDT is available.");
        }

        if (OutstandingPrincipal.Amount > 500_000m && string.IsNullOrWhiteSpace(arthaRinCaseNumber))
        {
            throw new InvariantViolationException(
                "ArthaRinSuitFilingMandatoryInvariant",
                "Artha Rin Adalat Ain 2003 & BRPD 01/2013",
                $"Cannot write off loan exceeding 500,000 BDT ({OutstandingPrincipal} BDT) without filing suit in Artha Rin Adalat.");
        }

        IsWrittenOff = true;
        LoanStatus = LoanAccountStatus.WrittenOff;
    }

    public void RecoverWrittenOff(
        Money recoveredAmount,
        out Money allocatedToPrincipal,
        out Money allocatedToInterest)
    {
        ArgumentNullException.ThrowIfNull(recoveredAmount, nameof(recoveredAmount));
        if (recoveredAmount.Amount <= 0)
        {
            throw new ArgumentException("Recovered amount must be positive.", nameof(recoveredAmount));
        }

        if (!IsWrittenOff)
        {
            throw new InvalidOperationException($"Loan account '{Id.Value}' is not written off. Use regular repayment waterfall.");
        }

        // Post-writeoff recovery waterfall:
        // 1. Recover Principal (credited to P&L Other Operating Income - Recovery of Written-off Loans)
        // 2. Recover Interest (credited to P&L Interest Income)
        decimal principalAlloc = Math.Min(recoveredAmount.Amount, OutstandingPrincipal.Amount);
        decimal interestAlloc = Math.Max(0m, recoveredAmount.Amount - principalAlloc);

        OutstandingPrincipal -= Money.Bdt(principalAlloc);

        allocatedToPrincipal = Money.Bdt(principalAlloc);
        allocatedToInterest = Money.Bdt(interestAlloc);

        if (OutstandingPrincipal.Amount == 0m)
        {
            LoanStatus = LoanAccountStatus.Closed;
        }
    }
}
