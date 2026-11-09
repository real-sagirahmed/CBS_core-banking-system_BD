using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.PaymentRails;

// ARCHITECTURAL INTENT: Bangladesh Automated Clearing House (BACH) Cheque Clearing Item Lifecycle
// enforcing High-Value (>= 500k) vs Regular-Value sessions, stale/post-dated checks, and official BB return codes (BRPD Circular 12/2024 Module 1.5.1, 1.5.2, 1.5.3).
public class ChequeClearingItem : AggregateRoot<Guid>
{
    public string BatchId { get; private set; }
    public ClearingDirection Direction { get; private set; }
    public ClearingSessionType Session { get; private set; }
    public MicrLine Micr { get; private set; }
    public Money Amount { get; private set; }
    public RoutingNumber PresentingBankRouting { get; private set; }
    public RoutingNumber DraweeBankRouting { get; private set; }
    public string BeneficiaryAccountNumber { get; private set; }
    public DateOnly ChequeIssueDate { get; private set; }
    public DateOnly PresentationDate { get; private set; }
    public ChequeClearingStatus Status { get; private set; }
    public BachReturnReason ReturnReason { get; private set; }
    public string? ReturnRemarks { get; private set; }
    public DateOnly? SettlementDate { get; private set; }

    public ChequeClearingItem(
        Guid id,
        string batchId,
        ClearingDirection direction,
        MicrLine micr,
        Money amount,
        RoutingNumber presentingBankRouting,
        RoutingNumber draweeBankRouting,
        string beneficiaryAccountNumber,
        DateOnly chequeIssueDate,
        DateOnly presentationDate) : base(id)
    {
        if (string.IsNullOrWhiteSpace(batchId)) throw new ArgumentException("Batch ID is required.", nameof(batchId));
        if (string.IsNullOrWhiteSpace(beneficiaryAccountNumber)) throw new ArgumentException("Beneficiary account is required.", nameof(beneficiaryAccountNumber));
        ArgumentNullException.ThrowIfNull(micr, nameof(micr));
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(presentingBankRouting, nameof(presentingBankRouting));
        ArgumentNullException.ThrowIfNull(draweeBankRouting, nameof(draweeBankRouting));

        if (amount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidChequeAmountInvariant",
                "BRPD Circular 12/2024 (1.5.2)",
                $"Cheque clearing amount must be strictly positive (provided: {amount.Amount}).");
        }

        // Automatic Session Classification (BRPD 1.5.1)
        // High-Value: >= 500,000 BDT; Regular-Value: < 500,000 BDT
        var session = amount >= Money.Bdt(500000m)
            ? ClearingSessionType.HighValue
            : ClearingSessionType.RegularValue;

        BatchId = batchId.Trim();
        Direction = direction;
        Session = session;
        Micr = micr;
        Amount = amount;
        PresentingBankRouting = presentingBankRouting;
        DraweeBankRouting = draweeBankRouting;
        BeneficiaryAccountNumber = beneficiaryAccountNumber.Trim();
        ChequeIssueDate = chequeIssueDate;
        PresentationDate = presentationDate;
        Status = ChequeClearingStatus.Presented;
        ReturnReason = BachReturnReason.None;

        // Check Stale or Post-Dated validity (BRPD 1.5.3 Code 05)
        if (presentationDate < chequeIssueDate)
        {
            // Post-dated cheque
            ReturnCheque(BachReturnReason.PostDatedOrStaleCheque, $"Post-dated cheque (Cheque date: {chequeIssueDate:yyyy-MM-dd}, Presented: {presentationDate:yyyy-MM-dd}).");
        }
        else if (presentationDate > chequeIssueDate.AddDays(180))
        {
            // Stale cheque (> 6 months / 180 days in Bangladesh)
            ReturnCheque(BachReturnReason.PostDatedOrStaleCheque, $"Stale cheque exceeding 180-day statutory validity (Cheque date: {chequeIssueDate:yyyy-MM-dd}).");
        }
    }

    public void Settle(DateOnly settlementDate)
    {
        if (Status == ChequeClearingStatus.Settled)
        {
            throw new InvariantViolationException(
                "ChequeAlreadySettledInvariant",
                "BRPD Circular 12/2024 (1.5.2)",
                $"Cheque clearing item '{Id}' is already settled on {SettlementDate:yyyy-MM-dd}.");
        }

        if (Status == ChequeClearingStatus.Returned)
        {
            throw new InvariantViolationException(
                "ReturnedChequeSettlementBlockedInvariant",
                "BRPD Circular 12/2024 (1.5.3)",
                $"Cannot settle cheque clearing item '{Id}' because it was returned (Reason: {ReturnReason}).");
        }

        Status = ChequeClearingStatus.Settled;
        SettlementDate = settlementDate;
    }

    public void ReturnCheque(BachReturnReason reason, string remarks)
    {
        if (reason == BachReturnReason.None)
        {
            throw new ArgumentException("A valid Bangladesh Bank return reason must be specified.", nameof(reason));
        }

        if (Status == ChequeClearingStatus.Settled)
        {
            throw new InvariantViolationException(
                "SettledChequeReturnProhibitedInvariant",
                "BRPD Circular 12/2024 (1.5.3)",
                $"Cannot return cheque clearing item '{Id}' after final settlement at Bangladesh Bank.");
        }

        Status = ChequeClearingStatus.Returned;
        ReturnReason = reason;
        ReturnRemarks = remarks.Trim();
    }
}
