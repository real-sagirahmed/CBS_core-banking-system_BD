using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.PaymentRails;

// ARCHITECTURAL INTENT: Bangladesh Electronic Funds Transfer Network (BEFTN) Batch Item Lifecycle
// for interbank direct credit and debit transfers (BRPD Circular 12/2024 Module 1.5.4).
public class BeftnBatchItem : AggregateRoot<Guid>
{
    public string BatchId { get; private set; }
    public ClearingDirection Direction { get; private set; }
    public BeftnTransactionType Type { get; private set; }
    public Money Amount { get; private set; }
    public RoutingNumber OriginatingRouting { get; private set; }
    public string OriginatingAccount { get; private set; }
    public RoutingNumber DestinationRouting { get; private set; }
    public string DestinationAccount { get; private set; }
    public string BeneficiaryName { get; private set; }
    public string PaymentReference { get; private set; }
    public BeftnStatus Status { get; private set; }
    public DateOnly SubmissionDate { get; private set; }
    public DateOnly? SettlementDate { get; private set; }
    public string? ReturnCode { get; private set; }
    public string? ReturnReason { get; private set; }

    // Parameterless constructor for EF Core ORM materialization
    private BeftnBatchItem() : base(Guid.Empty) { }

    public BeftnBatchItem(
        Guid id,
        string batchId,
        ClearingDirection direction,
        BeftnTransactionType type,
        Money amount,
        RoutingNumber originatingRouting,
        string originatingAccount,
        RoutingNumber destinationRouting,
        string destinationAccount,
        string beneficiaryName,
        string paymentReference,
        DateOnly submissionDate) : base(id)
    {
        if (string.IsNullOrWhiteSpace(batchId)) throw new ArgumentException("Batch ID is required.", nameof(batchId));
        if (string.IsNullOrWhiteSpace(originatingAccount)) throw new ArgumentException("Originating account is required.", nameof(originatingAccount));
        if (string.IsNullOrWhiteSpace(destinationAccount)) throw new ArgumentException("Destination account is required.", nameof(destinationAccount));
        if (string.IsNullOrWhiteSpace(beneficiaryName)) throw new ArgumentException("Beneficiary name is required.", nameof(beneficiaryName));
        if (string.IsNullOrWhiteSpace(paymentReference)) throw new ArgumentException("Payment reference is required.", nameof(paymentReference));
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(originatingRouting, nameof(originatingRouting));
        ArgumentNullException.ThrowIfNull(destinationRouting, nameof(destinationRouting));

        if (amount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidBeftnAmountInvariant",
                "BRPD Circular 12/2024 (1.5.4)",
                $"BEFTN transfer amount must be strictly positive (provided: {amount.Amount}).");
        }

        BatchId = batchId.Trim();
        Direction = direction;
        Type = type;
        Amount = amount;
        OriginatingRouting = originatingRouting;
        OriginatingAccount = originatingAccount.Trim();
        DestinationRouting = destinationRouting;
        DestinationAccount = destinationAccount.Trim();
        BeneficiaryName = beneficiaryName.Trim();
        PaymentReference = paymentReference.Trim();
        Status = BeftnStatus.Queued;
        SubmissionDate = submissionDate;
    }

    public void SendToBangladeshBank()
    {
        if (Status != BeftnStatus.Queued)
        {
            throw new InvariantViolationException(
                "InvalidBeftnStateTransitionInvariant",
                "BRPD Circular 12/2024 (1.5.4)",
                $"Cannot transmit BEFTN item '{Id}' with status '{Status}'. Only 'Queued' items can be sent to Bangladesh Bank.");
        }

        Status = BeftnStatus.SentToBb;
    }

    public void Settle(DateOnly settlementDate)
    {
        if (Status == BeftnStatus.Settled)
        {
            throw new InvariantViolationException(
                "BeftnAlreadySettledInvariant",
                "BRPD Circular 12/2024 (1.5.4)",
                $"BEFTN item '{Id}' is already settled.");
        }

        if (Status == BeftnStatus.Returned)
        {
            throw new InvariantViolationException(
                "ReturnedBeftnSettlementBlockedInvariant",
                "BRPD Circular 12/2024 (1.5.4)",
                $"Cannot settle returned BEFTN item '{Id}'.");
        }

        Status = BeftnStatus.Settled;
        SettlementDate = settlementDate;
    }

    public void ReturnTransfer(string returnCode, string reason)
    {
        if (string.IsNullOrWhiteSpace(returnCode)) throw new ArgumentException("Return code is required.", nameof(returnCode));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Return reason is required.", nameof(reason));

        if (Status == BeftnStatus.Settled)
        {
            throw new InvariantViolationException(
                "SettledBeftnReturnProhibitedInvariant",
                "BRPD Circular 12/2024 (1.5.4)",
                $"Cannot return BEFTN item '{Id}' after final settlement at Bangladesh Bank.");
        }

        Status = BeftnStatus.Returned;
        ReturnCode = returnCode.Trim();
        ReturnReason = reason.Trim();
    }
}
