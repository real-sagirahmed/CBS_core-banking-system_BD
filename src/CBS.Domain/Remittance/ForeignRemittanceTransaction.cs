using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Remittance.ValueObjects;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Remittance;

// ARCHITECTURAL INTENT: Foreign Remittance (Inward SWIFT MT103 & Outward TT),
// Wage Earners' 2.5% Government Cash Incentive Engine, Dynamic Entitlement Quota Enforcement,
// Pre-Credit Sanction Screening, and Irreversible Outward Authorization Lock
// (BRPD Circular 12/2024 Module 2.2, Guidelines for Foreign Exchange Transactions & BB FE Circular No. 01/2022).
public class ForeignRemittanceTransaction : AggregateRoot<Guid>
{
    public string TransactionReference { get; private set; }
    public ForeignRemittanceDirection Direction { get; private set; }
    public ForeignRemittanceType RemittanceType { get; private set; }
    public string SenderName { get; private set; }
    public string SenderCountry { get; private set; }
    public string? SenderBankBic { get; private set; }
    public string BeneficiaryName { get; private set; }
    public AccountNumber? BeneficiaryAccountNumber { get; private set; }
    public CifNumber? BeneficiaryCif { get; private set; }
    public Money ForeignAmount { get; private set; }
    public decimal ExchangeRate { get; private set; }
    public Money ConvertedBdtAmount { get; private set; }
    public decimal IncentiveRate { get; private set; } // 0.025 for Wage Earner
    public Money IncentiveAmount { get; private set; }
    public Money TotalDisbursedBdtAmount { get; private set; }
    public RemittanceChargeParty ChargeParty { get; private set; }
    public SwiftMt103Payload? SwiftMessage { get; private set; }
    public SanctionStatus SanctionStatus { get; private set; }
    public ForeignRemittanceStatus Status { get; private set; }
    public GovernmentIncentiveStatus IncentiveStatus { get; private set; }
    public Guid? CentralBankClaimBatchId { get; private set; }
    public string? PurposeOfRemittance { get; private set; }
    public string? QuotaReference { get; private set; }
    public decimal? QuotaLimitAnnualBdt { get; private set; }
    public decimal? QuotaUtilizedAnnualBdt { get; private set; }
    public string MakerUserId { get; private set; }
    public string? CheckerUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? AuthorizedAtUtc { get; private set; }
    public DateTime? DisbursedAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    #pragma warning disable CS8618
    protected ForeignRemittanceTransaction() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    // Internal complete constructor for ORM & factory initialization
    public ForeignRemittanceTransaction(
        Guid id,
        string transactionReference,
        ForeignRemittanceDirection direction,
        ForeignRemittanceType remittanceType,
        string senderName,
        string senderCountry,
        string? senderBankBic,
        string beneficiaryName,
        AccountNumber? beneficiaryAccountNumber,
        CifNumber? beneficiaryCif,
        Money foreignAmount,
        decimal exchangeRate,
        RemittanceChargeParty chargeParty,
        string makerUserId,
        DateTime createdAtUtc,
        SwiftMt103Payload? swiftMessage = null,
        SanctionStatus sanctionStatus = SanctionStatus.Clean,
        string? purposeOfRemittance = null,
        string? quotaReference = null,
        decimal? quotaLimitAnnualBdt = null,
        decimal? quotaUtilizedAnnualBdt = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(transactionReference))
            throw new ArgumentException("Transaction reference is required.", nameof(transactionReference));
        if (string.IsNullOrWhiteSpace(senderName))
            throw new ArgumentException("Sender name is required.", nameof(senderName));
        if (string.IsNullOrWhiteSpace(senderCountry))
            throw new ArgumentException("Sender country is required.", nameof(senderCountry));
        if (string.IsNullOrWhiteSpace(beneficiaryName))
            throw new ArgumentException("Beneficiary name is required.", nameof(beneficiaryName));
        if (string.IsNullOrWhiteSpace(makerUserId))
            throw new ArgumentException("Maker user ID is required.", nameof(makerUserId));
        ArgumentNullException.ThrowIfNull(foreignAmount, nameof(foreignAmount));

        if (foreignAmount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidForeignAmountInvariant",
                "BRPD Circular 12/2024 (2.2.1, 2.2.2)",
                $"Foreign remittance amount must be strictly positive (provided: {foreignAmount.Amount}).");
        }

        if (exchangeRate <= 0)
        {
            throw new InvariantViolationException(
                "InvalidExchangeRateInvariant",
                "BRPD Circular 12/2024 (2.2.2-ii)",
                $"Treasury exchange rate must be strictly positive (provided: {exchangeRate}).");
        }

        TransactionReference = transactionReference.Trim().ToUpperInvariant();
        Direction = direction;
        RemittanceType = remittanceType;
        SenderName = senderName.Trim();
        SenderCountry = senderCountry.Trim().ToUpperInvariant();
        SenderBankBic = senderBankBic?.Trim().ToUpperInvariant();
        BeneficiaryName = beneficiaryName.Trim();
        BeneficiaryAccountNumber = beneficiaryAccountNumber;
        BeneficiaryCif = beneficiaryCif;
        ForeignAmount = foreignAmount;
        ExchangeRate = exchangeRate;
        ChargeParty = chargeParty;
        MakerUserId = makerUserId.Trim();
        CreatedAtUtc = createdAtUtc;
        SwiftMessage = swiftMessage;
        SanctionStatus = sanctionStatus;
        PurposeOfRemittance = purposeOfRemittance?.Trim();
        QuotaReference = quotaReference?.Trim();
        QuotaLimitAnnualBdt = quotaLimitAnnualBdt;
        QuotaUtilizedAnnualBdt = quotaUtilizedAnnualBdt;

        // Mathematical conversion to BDT
        decimal convertedAmount = Math.Round(foreignAmount.Amount * exchangeRate, 2, MidpointRounding.AwayFromZero);
        ConvertedBdtAmount = Money.Bdt(convertedAmount);

        // Dynamic Quota Validation for Outward Student/Medical Remittances (BRPD 2.2.6-i)
        if (direction == ForeignRemittanceDirection.Outward && quotaLimitAnnualBdt.HasValue)
        {
            decimal cumulative = (quotaUtilizedAnnualBdt ?? 0m) + ConvertedBdtAmount.Amount;
            if (cumulative > quotaLimitAnnualBdt.Value)
            {
                throw new InvariantViolationException(
                    "OutwardEntitlementQuotaExceededInvariant",
                    "BRPD Circular 12/2024 (2.2.6-i)",
                    $"Outward remittance of {ConvertedBdtAmount.Amount:N2} BDT breaches remaining annual entitlement quota of {(quotaLimitAnnualBdt.Value - (quotaUtilizedAnnualBdt ?? 0m)):N2} BDT.");
            }
        }

        // Wage Earners' 2.5% Statutory Government Cash Incentive Engine (BRPD 2.2.3 & BB FE Circular 01/2022)
        if (direction == ForeignRemittanceDirection.Inward && remittanceType == ForeignRemittanceType.WageEarnerInward)
        {
            IncentiveRate = 0.025m;
            decimal incentiveBdt = Math.Round(ConvertedBdtAmount.Amount * 0.025m, 2, MidpointRounding.AwayFromZero);
            IncentiveAmount = Money.Bdt(incentiveBdt);
            IncentiveStatus = GovernmentIncentiveStatus.Calculated;
        }
        else
        {
            IncentiveRate = 0.00m;
            IncentiveAmount = Money.Bdt(0m);
            IncentiveStatus = GovernmentIncentiveStatus.NotApplicable;
        }

        TotalDisbursedBdtAmount = ConvertedBdtAmount + IncentiveAmount;

        // Pre-Settlement Sanction Screening Enforcement (BRPD 2.2.1-viii, 2.2.2-vii)
        if (sanctionStatus == SanctionStatus.ConfirmedSanctionHit)
        {
            Status = ForeignRemittanceStatus.HeldForSanctionReview;
        }
        else
        {
            Status = ForeignRemittanceStatus.PendingCheckerApproval;
        }
    }

    /// <summary>
    /// Factory for creating an Inward Remittance Transaction (e.g. from SWIFT MT103 ingest).
    /// </summary>
    public static ForeignRemittanceTransaction CreateInward(
        Guid id,
        string transactionReference,
        ForeignRemittanceType remittanceType,
        string senderName,
        string senderCountry,
        string? senderBankBic,
        string beneficiaryName,
        AccountNumber beneficiaryAccountNumber,
        CifNumber? beneficiaryCif,
        Money foreignAmount,
        decimal exchangeRate,
        RemittanceChargeParty chargeParty,
        string makerUserId,
        DateTime createdAtUtc,
        SwiftMt103Payload? swiftMessage = null,
        SanctionStatus sanctionStatus = SanctionStatus.Clean,
        string? purposeOfRemittance = null)
    {
        return new ForeignRemittanceTransaction(
            id,
            transactionReference,
            ForeignRemittanceDirection.Inward,
            remittanceType,
            senderName,
            senderCountry,
            senderBankBic,
            beneficiaryName,
            beneficiaryAccountNumber,
            beneficiaryCif,
            foreignAmount,
            exchangeRate,
            chargeParty,
            makerUserId,
            createdAtUtc,
            swiftMessage,
            sanctionStatus,
            purposeOfRemittance);
    }

    /// <summary>
    /// Factory for initiating an Outward Remittance Transaction with dynamic quota evaluation.
    /// </summary>
    public static ForeignRemittanceTransaction CreateOutward(
        Guid id,
        string transactionReference,
        ForeignRemittanceType remittanceType,
        string senderName,
        string senderCountry,
        string? senderBankBic,
        string beneficiaryName,
        AccountNumber? senderDebitAccountNumber,
        CifNumber? senderCif,
        Money foreignAmount,
        decimal exchangeRate,
        RemittanceChargeParty chargeParty,
        string makerUserId,
        DateTime createdAtUtc,
        SwiftMt103Payload? swiftMessage = null,
        SanctionStatus sanctionStatus = SanctionStatus.Clean,
        string? purposeOfRemittance = null,
        string? quotaReference = null,
        decimal? quotaLimitAnnualBdt = null,
        decimal? quotaUtilizedAnnualBdt = null)
    {
        return new ForeignRemittanceTransaction(
            id,
            transactionReference,
            ForeignRemittanceDirection.Outward,
            remittanceType,
            senderName,
            senderCountry,
            senderBankBic,
            beneficiaryName,
            senderDebitAccountNumber,
            senderCif,
            foreignAmount,
            exchangeRate,
            chargeParty,
            makerUserId,
            createdAtUtc,
            swiftMessage,
            sanctionStatus,
            purposeOfRemittance,
            quotaReference,
            quotaLimitAnnualBdt,
            quotaUtilizedAnnualBdt);
    }

    /// <summary>
    /// Dual-control checker authorization for Inward Remittance, releasing funds and government incentive.
    /// </summary>
    public void AuthorizeInwardRemittance(string checkerUserId, DateTime authorizedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));

        // Maker-Checker Separation Invariant (BRPD 1.3.18, 10.3.5-x)
        if (checkerUserId.Equals(MakerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Maker '{MakerUserId}' cannot authorize their own inward remittance transaction.");
        }

        // Sanction screening hard gate (BRPD 2.2.2-vii)
        if (SanctionStatus == SanctionStatus.ConfirmedSanctionHit)
        {
            throw new InvariantViolationException(
                "SanctionScreeningHitInvariant",
                "BRPD Circular 12/2024 (2.2.2-vii)",
                $"Remittance '{TransactionReference}' has a confirmed sanction hit and cannot be credited to beneficiary.");
        }

        if (Status != ForeignRemittanceStatus.PendingCheckerApproval)
        {
            throw new InvariantViolationException(
                "InvalidRemittanceStatusForAuthorization",
                "BRPD Circular 12/2024 (2.2.3-iii)",
                $"Cannot authorize inward remittance in status '{Status}'. Only PendingCheckerApproval items can be authorized.");
        }

        CheckerUserId = checkerUserId.Trim();
        AuthorizedAtUtc = authorizedAtUtc;
        DisbursedAtUtc = authorizedAtUtc;
        Status = ForeignRemittanceStatus.CreditedToBeneficiary;

        if (IncentiveStatus == GovernmentIncentiveStatus.Calculated)
        {
            IncentiveStatus = GovernmentIncentiveStatus.DisbursedToBeneficiary;
        }
    }

    /// <summary>
    /// Dual-control checker authorization for Outward Remittance, locking instruction and generating SWIFT transmission.
    /// </summary>
    public void AuthorizeOutwardRemittance(string checkerUserId, DateTime authorizedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));

        if (checkerUserId.Equals(MakerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Maker '{MakerUserId}' cannot authorize their own outward remittance transaction.");
        }

        if (SanctionStatus == SanctionStatus.ConfirmedSanctionHit)
        {
            throw new InvariantViolationException(
                "SanctionScreeningHitInvariant",
                "BRPD Circular 12/2024 (2.2.1-viii)",
                $"Remittance '{TransactionReference}' has a confirmed sanction hit and cannot be transmitted.");
        }

        if (Status != ForeignRemittanceStatus.PendingCheckerApproval)
        {
            throw new InvariantViolationException(
                "InvalidRemittanceStatusForAuthorization",
                "BRPD Circular 12/2024 (2.2.1-vii)",
                $"Cannot authorize outward remittance in status '{Status}'. Only PendingCheckerApproval items can be authorized.");
        }

        CheckerUserId = checkerUserId.Trim();
        AuthorizedAtUtc = authorizedAtUtc;
        Status = ForeignRemittanceStatus.SwiftTransmitted;
    }

    /// <summary>
    /// Outward or Inward Remittance Cancellation with inviolable post-authorization lock.
    /// </summary>
    public void Cancel(string reason, string requestedByUserId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required.", nameof(reason));

        // ARCHITECTURAL INTENT: Outward/Inward Remittance cannot be cancelled after Checker Authorization (BRPD 2.2.1-vii-f, g)
        if (Status == ForeignRemittanceStatus.SwiftTransmitted ||
            Status == ForeignRemittanceStatus.CreditedToBeneficiary ||
            Status == ForeignRemittanceStatus.ApprovedPendingSettlement)
        {
            throw new InvariantViolationException(
                "IrreversibleOutwardRemittanceLock",
                "BRPD Circular 12/2024 (2.2.1-vii-f, g)",
                $"Remittance transaction '{TransactionReference}' is in status '{Status}' and cannot be cancelled after authorization or transmission.");
        }

        Status = ForeignRemittanceStatus.Cancelled;
        CancellationReason = reason.Trim();
    }

    /// <summary>
    /// Marks the disbursed incentive as batched into a central bank reimbursement claim.
    /// </summary>
    public void MarkIncentiveBatched(Guid claimBatchId)
    {
        if (IncentiveStatus != GovernmentIncentiveStatus.DisbursedToBeneficiary)
        {
            throw new InvariantViolationException(
                "InvalidIncentiveStatusForBatching",
                "BRPD Circular 12/2024 (2.2.3)",
                $"Only disbursed incentives can be batched for central bank claim (current status: '{IncentiveStatus}').");
        }

        CentralBankClaimBatchId = claimBatchId;
        IncentiveStatus = GovernmentIncentiveStatus.ClaimBatched;
    }

    /// <summary>
    /// Marks the government incentive as reimbursed by Bangladesh Bank.
    /// </summary>
    public void MarkIncentiveReimbursed()
    {
        if (IncentiveStatus != GovernmentIncentiveStatus.ClaimBatched)
        {
            throw new InvariantViolationException(
                "InvalidIncentiveStatusForReimbursement",
                "BRPD Circular 12/2024 (2.2.3)",
                $"Only batched incentive claims can be marked reimbursed (current status: '{IncentiveStatus}').");
        }

        IncentiveStatus = GovernmentIncentiveStatus.ReimbursedByCentralBank;
    }
}
