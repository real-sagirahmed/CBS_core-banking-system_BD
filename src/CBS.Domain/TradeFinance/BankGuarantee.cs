using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.TradeFinance;

// ARCHITECTURAL INTENT: Bank Guarantee (BG) Contingent Liability Lifecycle,
// cash margin / deposit lien integration, tiered quarterly commission with 15% NBR VAT,
// amendment, claim encashment, and cancellation conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 5.4.
public class BankGuarantee : AggregateRoot<Guid>
{
    public string BgNumber { get; private set; }
    public GuaranteeType Type { get; private set; }
    public CifNumber ApplicantCif { get; private set; }
    public string BeneficiaryName { get; private set; }
    public string BeneficiaryAddress { get; private set; }
    public string IssuingBranchCode { get; private set; }

    public Money GuaranteeAmount { get; private set; }
    public Money CashMarginAmount { get; private set; }
    public string? CollateralDepositAccountNumber { get; private set; } // Linked FDR/DPS Account for Lien
    public decimal MarginPercentage { get; private set; }

    public decimal CommissionRatePerQuarter { get; private set; }
    public Money CommissionAmount { get; private set; }
    public Money VatAmount { get; private set; }
    public Money TotalFeePayable { get; private set; }

    public DateOnly IssueDate { get; private set; }
    public DateOnly ExpiryDate { get; private set; }
    public int ClaimPeriodDays { get; private set; }
    public GuaranteeExpiryType ExpiryType { get; private set; }
    public BankGuaranteeStatus Status { get; private set; }

    public string MakerUserId { get; private set; }
    public string CheckerUserId { get; private set; }
    public string MisSectorCode { get; private set; }

    public string? AmendmentReference { get; private set; }
    public string? ClaimLetterReference { get; private set; }
    public DateOnly? ClaimDate { get; private set; }
    public string? ReleaseLetterReference { get; private set; }
    public bool OriginalReturned { get; private set; }
    public DateOnly? CancelledDate { get; private set; }

    #pragma warning disable CS8618
    private BankGuarantee() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public BankGuarantee(
        Guid id,
        string bgNumber,
        GuaranteeType type,
        CifNumber applicantCif,
        string beneficiaryName,
        string beneficiaryAddress,
        string issuingBranchCode,
        Money guaranteeAmount,
        decimal marginPercentage,
        string? collateralDepositAccount,
        decimal commissionRatePerQuarter,
        DateOnly issueDate,
        DateOnly expiryDate,
        int claimPeriodDays,
        GuaranteeExpiryType expiryType,
        string makerUserId,
        string checkerUserId,
        string misSectorCode = "SEC-TRADE-BG") : base(id)
    {
        if (string.IsNullOrWhiteSpace(bgNumber)) throw new ArgumentException("BG number is required.", nameof(bgNumber));
        if (string.IsNullOrWhiteSpace(beneficiaryName)) throw new ArgumentException("Beneficiary name is required.", nameof(beneficiaryName));
        if (string.IsNullOrWhiteSpace(beneficiaryAddress)) throw new ArgumentException("Beneficiary address is required.", nameof(beneficiaryAddress));
        if (string.IsNullOrWhiteSpace(issuingBranchCode)) throw new ArgumentException("Issuing branch code is required.", nameof(issuingBranchCode));
        if (string.IsNullOrWhiteSpace(makerUserId)) throw new ArgumentException("Maker User ID is required.", nameof(makerUserId));
        if (string.IsNullOrWhiteSpace(checkerUserId)) throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));
        ArgumentNullException.ThrowIfNull(applicantCif, nameof(applicantCif));
        ArgumentNullException.ThrowIfNull(guaranteeAmount, nameof(guaranteeAmount));

        if (guaranteeAmount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidGuaranteeAmountInvariant",
                "BRPD Circular 12/2024 (5.4.1)",
                $"Guarantee amount must be strictly positive (provided: {guaranteeAmount.Amount}).");
        }

        if (expiryDate <= issueDate)
        {
            throw new InvariantViolationException(
                "InvalidGuaranteeExpiryDateInvariant",
                "BRPD Circular 12/2024 (5.4.1-i)",
                $"Guarantee expiry date ({expiryDate}) must be strictly after issue date ({issueDate}).");
        }

        // Maker-Checker Separation Invariant (BRPD 1.3.18, 10.3.5-x)
        if (makerUserId.Equals(checkerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Maker '{makerUserId}' cannot authorize their own Bank Guarantee issuance.");
        }

        if (marginPercentage < 0 || marginPercentage > 100)
        {
            throw new InvariantViolationException(
                "InvalidMarginPercentageInvariant",
                "BRPD Circular 12/2024 (5.4.1-ix)",
                $"Margin percentage must be between 0% and 100% (provided: {marginPercentage}%).");
        }

        // Compute Cash Margin
        decimal marginAmount = Math.Round(guaranteeAmount.Amount * (marginPercentage / 100m), 2, MidpointRounding.AwayFromZero);
        var cashMargin = Money.Bdt(marginAmount);

        // Compute Quarterly Commission (BRPD 5.4.1-xi)
        int totalDays = (expiryDate.DayNumber - issueDate.DayNumber) + claimPeriodDays;
        int quarters = Math.Max(1, (int)Math.Ceiling(totalDays / 90.0));
        decimal baseCommission = Math.Round(guaranteeAmount.Amount * (commissionRatePerQuarter / 100m) * quarters, 2, MidpointRounding.AwayFromZero);
        decimal commissionAmt = Math.Max(1000m, baseCommission); // Minimum 1000 BDT
        var commission = Money.Bdt(commissionAmt);

        // 15% Statutory NBR VAT (BRPD 1.5.6-v, 5.4.1)
        decimal vatAmt = Math.Round(commissionAmt * 0.15m, 2, MidpointRounding.AwayFromZero);
        var vat = Money.Bdt(vatAmt);

        BgNumber = bgNumber.Trim().ToUpperInvariant();
        Type = type;
        ApplicantCif = applicantCif;
        BeneficiaryName = beneficiaryName.Trim();
        BeneficiaryAddress = beneficiaryAddress.Trim();
        IssuingBranchCode = issuingBranchCode.Trim();
        GuaranteeAmount = guaranteeAmount;
        CashMarginAmount = cashMargin;
        CollateralDepositAccountNumber = collateralDepositAccount?.Trim();
        MarginPercentage = marginPercentage;
        CommissionRatePerQuarter = commissionRatePerQuarter;
        CommissionAmount = commission;
        VatAmount = vat;
        TotalFeePayable = commission + vat;
        IssueDate = issueDate;
        ExpiryDate = expiryDate;
        ClaimPeriodDays = claimPeriodDays;
        ExpiryType = expiryType;
        Status = BankGuaranteeStatus.Issued;
        MakerUserId = makerUserId.Trim();
        CheckerUserId = checkerUserId.Trim();
        MisSectorCode = misSectorCode.Trim();
    }

    /// <summary>
    /// Amends guarantee for amount enhancement and/or validity extension (BRPD 5.4.1-ii).
    /// </summary>
    public void AmendGuarantee(
        Money? additionalAmount,
        DateOnly? extendedExpiryDate,
        string approvalReference,
        string checkerUserId)
    {
        if (string.IsNullOrWhiteSpace(approvalReference)) throw new ArgumentException("Approval reference is required.", nameof(approvalReference));
        if (string.IsNullOrWhiteSpace(checkerUserId)) throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));

        if (Status != BankGuaranteeStatus.Issued && Status != BankGuaranteeStatus.Amended)
        {
            throw new InvariantViolationException(
                "InvalidBgStatusForAmendmentInvariant",
                "BRPD Circular 12/2024 (5.4.1-ii)",
                $"Cannot amend Bank Guarantee '{BgNumber}' with status '{Status}'.");
        }

        if (additionalAmount != null && additionalAmount.Amount > 0)
        {
            GuaranteeAmount += additionalAmount;
            decimal additionalMargin = Math.Round(additionalAmount.Amount * (MarginPercentage / 100m), 2, MidpointRounding.AwayFromZero);
            CashMarginAmount += Money.Bdt(additionalMargin);

            // Additional commission for enhanced amount
            int remainingDays = (ExpiryDate.DayNumber - IssueDate.DayNumber) + ClaimPeriodDays;
            int quarters = Math.Max(1, (int)Math.Ceiling(remainingDays / 90.0));
            decimal addCommission = Math.Round(additionalAmount.Amount * (CommissionRatePerQuarter / 100m) * quarters, 2, MidpointRounding.AwayFromZero);
            var addComm = Money.Bdt(addCommission);
            decimal addVat = Math.Round(addCommission * 0.15m, 2, MidpointRounding.AwayFromZero);
            CommissionAmount += addComm;
            VatAmount += Money.Bdt(addVat);
            TotalFeePayable = CommissionAmount + VatAmount;
        }

        if (extendedExpiryDate != null)
        {
            if (extendedExpiryDate.Value <= ExpiryDate)
            {
                throw new InvariantViolationException(
                    "InvalidExtendedExpiryDateInvariant",
                    "BRPD Circular 12/2024 (5.4.1-ii)",
                    $"Extended expiry date ({extendedExpiryDate.Value}) must be after current expiry date ({ExpiryDate}).");
            }

            int extendedDays = extendedExpiryDate.Value.DayNumber - ExpiryDate.DayNumber;
            int extraQuarters = Math.Max(1, (int)Math.Ceiling(extendedDays / 90.0));
            decimal extraCommission = Math.Round(GuaranteeAmount.Amount * (CommissionRatePerQuarter / 100m) * extraQuarters, 2, MidpointRounding.AwayFromZero);
            var extraComm = Money.Bdt(extraCommission);
            decimal extraVat = Math.Round(extraCommission * 0.15m, 2, MidpointRounding.AwayFromZero);

            CommissionAmount += extraComm;
            VatAmount += Money.Bdt(extraVat);
            TotalFeePayable = CommissionAmount + VatAmount;

            ExpiryDate = extendedExpiryDate.Value;
        }

        Status = BankGuaranteeStatus.Amended;
        AmendmentReference = approvalReference.Trim();
    }

    /// <summary>
    /// Beneficiary invokes claim against the Bank Guarantee (BRPD 5.4.1-iv).
    /// </summary>
    public void InvokeClaim(string claimLetterReference, DateOnly claimDate, string supervisorUserId)
    {
        if (string.IsNullOrWhiteSpace(claimLetterReference)) throw new ArgumentException("Claim letter reference is required.", nameof(claimLetterReference));
        if (string.IsNullOrWhiteSpace(supervisorUserId)) throw new ArgumentException("Supervisor User ID is required.", nameof(supervisorUserId));

        if (Status != BankGuaranteeStatus.Issued && Status != BankGuaranteeStatus.Amended)
        {
            throw new InvariantViolationException(
                "InvalidBgStatusForClaimInvariant",
                "BRPD Circular 12/2024 (5.4.1-iv)",
                $"Cannot invoke claim on Bank Guarantee '{BgNumber}' with status '{Status}'.");
        }

        var maxClaimDate = ExpiryDate.AddDays(ClaimPeriodDays);
        if (claimDate > maxClaimDate)
        {
            throw new InvariantViolationException(
                "GuaranteeClaimPeriodExpiredInvariant",
                "BRPD Circular 12/2024 (5.4.1-iv)",
                $"Claim date ({claimDate}) exceeds maximum claim period boundary ({maxClaimDate}). Guarantee has lapsed.");
        }

        Status = BankGuaranteeStatus.ClaimInvoked;
        ClaimLetterReference = claimLetterReference.Trim();
        ClaimDate = claimDate;
    }

    /// <summary>
    /// Liquidates guarantee claim by paying out funds to the beneficiary authority (BRPD 5.4.1-iv).
    /// </summary>
    public Money LiquidateClaim(string supervisorUserId)
    {
        if (string.IsNullOrWhiteSpace(supervisorUserId)) throw new ArgumentException("Supervisor User ID is required.", nameof(supervisorUserId));

        if (Status != BankGuaranteeStatus.ClaimInvoked)
        {
            throw new InvariantViolationException(
                "InvalidBgStatusForLiquidationInvariant",
                "BRPD Circular 12/2024 (5.4.1-iv)",
                $"Cannot liquidate claim on Bank Guarantee '{BgNumber}' with status '{Status}'. Claim must be invoked first.");
        }

        Status = BankGuaranteeStatus.LiquidatedClaimPaid;
        return GuaranteeAmount;
    }

    /// <summary>
    /// Cancels guarantee upon surrender of original instrument or formal release memo, extinguishing contingent liability (BRPD 5.4.1-iii, xii, xiii).
    /// </summary>
    public void CancelAndRelease(string releaseLetterRef, bool originalReturned, DateOnly cancelDate, string supervisorUserId)
    {
        if (string.IsNullOrWhiteSpace(releaseLetterRef)) throw new ArgumentException("Release letter reference is required.", nameof(releaseLetterRef));
        if (string.IsNullOrWhiteSpace(supervisorUserId)) throw new ArgumentException("Supervisor User ID is required.", nameof(supervisorUserId));

        if (Status != BankGuaranteeStatus.Issued && Status != BankGuaranteeStatus.Amended && Status != BankGuaranteeStatus.ClaimInvoked)
        {
            throw new InvariantViolationException(
                "InvalidBgStatusForCancellationInvariant",
                "BRPD Circular 12/2024 (5.4.1-iii)",
                $"Cannot cancel Bank Guarantee '{BgNumber}' with status '{Status}'.");
        }

        Status = BankGuaranteeStatus.CancelledReleased;
        ReleaseLetterReference = releaseLetterRef.Trim();
        OriginalReturned = originalReturned;
        CancelledDate = cancelDate;
    }
}
