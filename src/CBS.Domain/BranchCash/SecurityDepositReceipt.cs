using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.BranchCash;

// ARCHITECTURAL INTENT: Security Deposit Receipt (SDR) lifecycle, tender/bid guarantees,
// duplicate issuance with indemnity bond, and stop payment revocation audit (BRPD Circular 12/2024 Modules 2.1.4, 2.1.1, 2.1.2).
public class SecurityDepositReceipt : AggregateRoot<Guid>
{
    public string SdrNumber { get; private set; }
    public string BranchCode { get; private set; }
    public string? PurchaserCif { get; private set; }
    public WalkInCustomerDetails? WalkInDetails { get; private set; }
    public string TenderOrRfpNumber { get; private set; }
    public string BeneficiaryAuthority { get; private set; }
    public Money Amount { get; private set; }
    public Money Commission { get; private set; }
    public Money VatAmount { get; private set; }
    public Money TotalPayable { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly ExpiryDate { get; private set; }
    public SecurityDepositReceiptStatus Status { get; private set; }
    public string MakerUserId { get; private set; }
    public string CheckerUserId { get; private set; }
    public string? ReleaseLetterReference { get; private set; }
    public string? ForfeitureClaimReference { get; private set; }
    public DateOnly? SettledDate { get; private set; }
    public string? StopReason { get; private set; }
    public string? StopRevocationReason { get; private set; }
    public string? DuplicateSdrNumber { get; private set; }
    public string? IndemnityBondReference { get; private set; }

    #pragma warning disable CS8618
    private SecurityDepositReceipt() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public SecurityDepositReceipt(
        Guid id,
        string sdrNumber,
        string branchCode,
        Money amount,
        string tenderOrRfpNumber,
        string beneficiaryAuthority,
        string? purchaserCif,
        WalkInCustomerDetails? walkInDetails,
        DateOnly issueDate,
        DateOnly expiryDate,
        string makerUserId,
        string checkerUserId) : base(id)
    {
        if (string.IsNullOrWhiteSpace(sdrNumber)) throw new ArgumentException("SDR number is required.", nameof(sdrNumber));
        if (string.IsNullOrWhiteSpace(branchCode)) throw new ArgumentException("Branch code is required.", nameof(branchCode));
        if (string.IsNullOrWhiteSpace(tenderOrRfpNumber)) throw new ArgumentException("Tender/RFP number is required.", nameof(tenderOrRfpNumber));
        if (string.IsNullOrWhiteSpace(beneficiaryAuthority)) throw new ArgumentException("Beneficiary authority is required.", nameof(beneficiaryAuthority));
        if (string.IsNullOrWhiteSpace(makerUserId)) throw new ArgumentException("Maker User ID is required.", nameof(makerUserId));
        if (string.IsNullOrWhiteSpace(checkerUserId)) throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));

        if (amount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidSdrAmountInvariant",
                "BRPD Circular 12/2024 (2.1.4)",
                $"SDR amount must be strictly positive (provided: {amount.Amount}).");
        }

        if (expiryDate <= issueDate)
        {
            throw new InvariantViolationException(
                "InvalidSdrExpiryDateInvariant",
                "BRPD Circular 12/2024 (2.1.4)",
                $"SDR expiry date ({expiryDate}) must be strictly after issue date ({issueDate}).");
        }

        // Maker-Checker Separation Invariant (BRPD 1.3.18, 10.3.5-x)
        if (makerUserId.Equals(checkerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Maker '{makerUserId}' cannot authorize their own SDR issuance.");
        }

        // Walk-in CDD enforcement for >= 50,000 BDT (BRPD 2.1.1-xii, 2.1.2-xiv)
        WalkInCustomerDetails.EnforceWalkInDueDiligence(amount, walkInDetails, purchaserCif);

        // Tiered Commission Calculation (BRPD 2.1.4-iv)
        decimal commissionAmt = CalculateSdrCommission(amount.Amount);
        var commission = Money.Bdt(commissionAmt);

        // Statutory 15% NBR VAT on Commission (BRPD 1.5.6-v, 2.1.4-iv)
        decimal vatAmt = Math.Round(commissionAmt * 0.15m, 2, MidpointRounding.AwayFromZero);
        var vat = Money.Bdt(vatAmt);

        SdrNumber = sdrNumber.Trim().ToUpperInvariant();
        BranchCode = branchCode.Trim();
        PurchaserCif = purchaserCif?.Trim();
        WalkInDetails = walkInDetails;
        TenderOrRfpNumber = tenderOrRfpNumber.Trim();
        BeneficiaryAuthority = beneficiaryAuthority.Trim();
        Amount = amount;
        Commission = commission;
        VatAmount = vat;
        TotalPayable = amount + commission + vat;
        IssueDate = issueDate;
        ExpiryDate = expiryDate;
        Status = SecurityDepositReceiptStatus.Issued;
        MakerUserId = makerUserId.Trim();
        CheckerUserId = checkerUserId.Trim();
    }

    public static decimal CalculateSdrCommission(decimal amount)
    {
        return amount switch
        {
            <= 50000m => 50m,
            <= 200000m => 100m,
            <= 1000000m => 250m,
            _ => 500m
        };
    }

    public void LienMark()
    {
        if (Status != SecurityDepositReceiptStatus.Issued)
        {
            throw new InvariantViolationException(
                "InvalidSdrStatusForLienInvariant",
                "BRPD Circular 12/2024 (2.1.4)",
                $"Cannot mark lien on SDR '{SdrNumber}' with status '{Status}'. Only 'Issued' SDR can be lien-marked.");
        }

        Status = SecurityDepositReceiptStatus.LienMarked;
    }

    /// <summary>
    /// Releases the SDR and refunds the principal to the purchaser upon formal release from the tender authority (BRPD 2.1.4-iii).
    /// </summary>
    public Money ReleaseAndRefund(string releaseLetterRef, DateOnly releaseDate, string supervisorUserId)
    {
        if (string.IsNullOrWhiteSpace(releaseLetterRef)) throw new ArgumentException("Release letter reference is required.", nameof(releaseLetterRef));
        if (string.IsNullOrWhiteSpace(supervisorUserId)) throw new ArgumentException("Supervisor User ID is required.", nameof(supervisorUserId));

        if (Status != SecurityDepositReceiptStatus.Issued && Status != SecurityDepositReceiptStatus.LienMarked)
        {
            throw new InvariantViolationException(
                "InvalidSdrStatusForReleaseInvariant",
                "BRPD Circular 12/2024 (2.1.4-iii)",
                $"Cannot release SDR '{SdrNumber}' with status '{Status}'. Only 'Issued' or 'LienMarked' SDR can be released.");
        }

        Status = SecurityDepositReceiptStatus.Refunded;
        ReleaseLetterReference = releaseLetterRef.Trim();
        SettledDate = releaseDate;

        return Amount;
    }

    /// <summary>
    /// Forfeits the SDR and liquidates funds directly to the beneficiary authority upon tender default/claim (BRPD 2.1.4).
    /// </summary>
    public Money ForfeitAndPayToBeneficiary(string claimLetterRef, DateOnly claimDate, string supervisorUserId)
    {
        if (string.IsNullOrWhiteSpace(claimLetterRef)) throw new ArgumentException("Claim letter reference is required.", nameof(claimLetterRef));
        if (string.IsNullOrWhiteSpace(supervisorUserId)) throw new ArgumentException("Supervisor User ID is required.", nameof(supervisorUserId));

        if (Status != SecurityDepositReceiptStatus.Issued && Status != SecurityDepositReceiptStatus.LienMarked)
        {
            throw new InvariantViolationException(
                "InvalidSdrStatusForClaimInvariant",
                "BRPD Circular 12/2024 (2.1.4)",
                $"Cannot pay forfeiture claim on SDR '{SdrNumber}' with status '{Status}'. Only 'Issued' or 'LienMarked' SDR can be claimed.");
        }

        Status = SecurityDepositReceiptStatus.ClaimPaid;
        ForfeitureClaimReference = claimLetterRef.Trim();
        SettledDate = claimDate;

        return Amount;
    }

    public void StopPayment(string reason, string supervisorUserId)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Stop payment reason is required.", nameof(reason));
        if (string.IsNullOrWhiteSpace(supervisorUserId)) throw new ArgumentException("Supervisor User ID is required.", nameof(supervisorUserId));

        if (Status != SecurityDepositReceiptStatus.Issued && Status != SecurityDepositReceiptStatus.LienMarked)
        {
            throw new InvariantViolationException(
                "InvalidSdrStatusForStopPaymentInvariant",
                "BRPD Circular 12/2024 (2.1.4)",
                $"Cannot place stop payment on SDR '{SdrNumber}' with status '{Status}'.");
        }

        Status = SecurityDepositReceiptStatus.Stopped;
        StopReason = reason.Trim();
    }

    /// <summary>
    /// Removes/revokes a stop payment on the SDR with mandatory narration (BRPD 2.1.1-ix, 2.1.2-vi).
    /// </summary>
    public void RevokeStopPayment(string removalReason, string supervisorUserId)
    {
        if (string.IsNullOrWhiteSpace(removalReason)) throw new ArgumentException("Stop removal reason is required.", nameof(removalReason));
        if (string.IsNullOrWhiteSpace(supervisorUserId)) throw new ArgumentException("Supervisor User ID is required.", nameof(supervisorUserId));

        if (Status != SecurityDepositReceiptStatus.Stopped)
        {
            throw new InvariantViolationException(
                "InvalidSdrStatusForStopRevocationInvariant",
                "BRPD Circular 12/2024 (2.1.1-ix, 2.1.2-vi)",
                $"Cannot revoke stop payment on SDR '{SdrNumber}' with status '{Status}'. Only 'Stopped' SDR can be revoked.");
        }

        Status = SecurityDepositReceiptStatus.Issued;
        StopRevocationReason = removalReason.Trim();
        StopReason = null;
    }

    /// <summary>
    /// Issues a duplicate SDR referencing the original instrument and secured by an indemnity bond (BRPD 2.1.4-ii).
    /// </summary>
    public void IssueDuplicate(string newSdrNumber, string indemnityBondReference, string supervisorUserId)
    {
        if (string.IsNullOrWhiteSpace(newSdrNumber)) throw new ArgumentException("New SDR number is required.", nameof(newSdrNumber));
        if (string.IsNullOrWhiteSpace(indemnityBondReference)) throw new ArgumentException("Indemnity bond reference is required.", nameof(indemnityBondReference));
        if (string.IsNullOrWhiteSpace(supervisorUserId)) throw new ArgumentException("Supervisor User ID is required.", nameof(supervisorUserId));

        if (Status != SecurityDepositReceiptStatus.Stopped)
        {
            throw new InvariantViolationException(
                "DuplicateSdrRequiresStopPaymentInvariant",
                "BRPD Circular 12/2024 (2.1.4-ii)",
                $"A duplicate SDR can only be issued against a stopped SDR backed by an indemnity bond (Current status: {Status}).");
        }

        Status = SecurityDepositReceiptStatus.DuplicateIssued;
        DuplicateSdrNumber = newSdrNumber.Trim().ToUpperInvariant();
        IndemnityBondReference = indemnityBondReference.Trim();
    }
}
