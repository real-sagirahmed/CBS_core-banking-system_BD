using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.BranchCash;

// ARCHITECTURAL INTENT: Demand Draft (DD) & Telegraphic Transfer (TT) Instrument Lifecycle
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 3.12 & 3.13.
// Enforces mandatory UNSCR & local sanction screening prior to issuance,
// automated cryptographic test key generation & verification,
// and systematic Contra Accounting entries upon issuance and settlement.
public class InterBranchPaymentInstrument : AggregateRoot<Guid>
{
    public string InstrumentNumber { get; private set; }
    public InterBranchInstrumentType Type { get; private set; }
    public Money Amount { get; private set; }
    public Money Commission { get; private set; }
    public Money VatAmount { get; private set; }
    public Money TotalPayable => Amount + Commission + VatAmount;

    public string IssuingBranchCode { get; private set; }
    public string PayeeBranchCode { get; private set; }
    public string BeneficiaryName { get; private set; }
    public string? BeneficiaryAccountNumber { get; private set; }
    public string RemitterName { get; private set; }

    public DateOnly IssueDate { get; private set; }
    public string CryptographicTestKey { get; private set; }
    public bool IsSanctionScreened { get; private set; }
    public bool ContraEntryPosted { get; private set; }
    public InterBranchInstrumentStatus Status { get; private set; }

    public DateOnly? PaidDate { get; private set; }
    public string? PaidByUserId { get; private set; }
    public string? CancelledByUserId { get; private set; }

    #pragma warning disable CS8618
    protected InterBranchPaymentInstrument() : base(Guid.NewGuid()) { }
    #pragma warning restore CS8618

    public InterBranchPaymentInstrument(
        string instrumentNumber,
        InterBranchInstrumentType type,
        Money amount,
        Money commission,
        Money vatAmount,
        string issuingBranchCode,
        string payeeBranchCode,
        string beneficiaryName,
        string remitterName,
        DateOnly issueDate,
        bool isSanctionScreened,
        string? beneficiaryAccountNumber = null) : base(Guid.NewGuid())
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(commission, nameof(commission));
        ArgumentNullException.ThrowIfNull(vatAmount, nameof(vatAmount));

        if (string.IsNullOrWhiteSpace(instrumentNumber))
            throw new ArgumentException("Instrument number required.", nameof(instrumentNumber));
        if (string.IsNullOrWhiteSpace(issuingBranchCode))
            throw new ArgumentException("Issuing branch code required.", nameof(issuingBranchCode));
        if (string.IsNullOrWhiteSpace(payeeBranchCode))
            throw new ArgumentException("Payee branch code required.", nameof(payeeBranchCode));
        if (string.IsNullOrWhiteSpace(beneficiaryName))
            throw new ArgumentException("Beneficiary name required.", nameof(beneficiaryName));
        if (amount.Amount <= 0)
            throw new ArgumentException("Instrument amount must be positive.", nameof(amount));

        // BRPD 3.12.1-vi, 3.13.1-vi: Sanction screening is mandatory before issuance
        if (!isSanctionScreened)
        {
            throw new InvariantViolationException(
                "RemittanceSanctionClearanceMandatoryInvariant",
                "BRPD Circular 12/2024 (3.12.1-vi, 3.13.1-vi)",
                "System shall ensure that customer is screened against UNSCR & local sanction before issuing instrument.");
        }

        InstrumentNumber = instrumentNumber.Trim();
        Type = type;
        Amount = amount;
        Commission = commission;
        VatAmount = vatAmount;
        IssuingBranchCode = issuingBranchCode.Trim();
        PayeeBranchCode = payeeBranchCode.Trim();
        BeneficiaryName = beneficiaryName.Trim();
        BeneficiaryAccountNumber = beneficiaryAccountNumber?.Trim();
        RemitterName = remitterName.Trim();
        IssueDate = issueDate;
        IsSanctionScreened = true;

        // Automatically derive 5-digit cryptographic test key
        CryptographicTestKey = CryptographicTestKeyService.GenerateTestKey(
            issueDate,
            amount.Amount,
            issuingBranchCode,
            payeeBranchCode);

        // BRPD 3.12.1-v: Contra entry is systematically initialized
        ContraEntryPosted = true;
        Status = InterBranchInstrumentStatus.IssuedPendingAdvice;
    }

    public void AcknowledgeAdviceReceived()
    {
        if (Status != InterBranchInstrumentStatus.IssuedPendingAdvice)
            return;
        Status = InterBranchInstrumentStatus.AdviceReceived;
    }

    // 1. INVARIANT: Test Key Verification and Payout (BRPD 3.12.2, 3.13.2-ii)
    public void VerifyAndPay(string providedTestKey, string payingUserId, DateOnly paymentDate)
    {
        if (string.IsNullOrWhiteSpace(payingUserId))
            throw new ArgumentException("Paying user id required.", nameof(payingUserId));

        if (Status == InterBranchInstrumentStatus.PaidSettled)
        {
            throw new InvariantViolationException(
                "InstrumentAlreadyPaidInvariant",
                "BRPD Circular 12/2024 (3.12.2)",
                $"Instrument '{InstrumentNumber}' has already been paid and settled on {PaidDate:yyyy-MM-dd}.");
        }

        if (Status == InterBranchInstrumentStatus.CancelledReversed)
        {
            throw new InvariantViolationException(
                "InstrumentCancelledInvariant",
                "BRPD Circular 12/2024 (3.12.2)",
                $"Instrument '{InstrumentNumber}' is cancelled.");
        }

        // BRPD 3.13.2-ii: Cryptographic Test Key verification
        bool isTestKeyValid = CryptographicTestKeyService.VerifyTestKey(
            providedTestKey,
            IssueDate,
            Amount.Amount,
            IssuingBranchCode,
            PayeeBranchCode);

        if (!isTestKeyValid)
        {
            Status = InterBranchInstrumentStatus.BlockedSecurityAlert;
            throw new InvariantViolationException(
                "TestKeyMismatchInvariant",
                "BRPD Circular 12/2024 (3.13.2-ii)",
                $"Cryptographic Test Key verification failed for instrument '{InstrumentNumber}'. Provided: '{providedTestKey}'. Payment blocked.");
        }

        Status = InterBranchInstrumentStatus.PaidSettled;
        PaidDate = paymentDate;
        PaidByUserId = payingUserId.Trim();
    }

    // 2. INVARIANT: Paid by Cancellation Advice & Refund (BRPD 3.12.2-i, ii)
    public Money CancelAndRefund(string authorizingUserId)
    {
        if (Status == InterBranchInstrumentStatus.PaidSettled)
        {
            throw new InvariantViolationException(
                "CannotCancelPaidInstrumentInvariant",
                "BRPD Circular 12/2024 (3.12.2)",
                $"Cannot cancel instrument '{InstrumentNumber}' because it is already paid and settled.");
        }

        Status = InterBranchInstrumentStatus.CancelledReversed;
        CancelledByUserId = authorizingUserId.Trim();
        return Amount;
    }
}
