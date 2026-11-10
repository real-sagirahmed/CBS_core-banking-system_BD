using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.PaymentRails;

// ARCHITECTURAL INTENT: Real-Time Gross Settlement (RTGS) ISO 20022 message lifecycle
// enforcing the 100,000 BDT regulatory customer threshold and instant central bank settlement (BRPD Circular 12/2024 Module 1.5.5).
public class RtgsTransaction : AggregateRoot<Guid>
{
    public string EndToEndId { get; private set; }
    public RtgsMessageType MessageType { get; private set; }
    public Money Amount { get; private set; }
    public RoutingNumber DebtorAgentRouting { get; private set; }
    public string DebtorAccount { get; private set; }
    public RoutingNumber CreditorAgentRouting { get; private set; }
    public string CreditorAccount { get; private set; }
    public string CreditorName { get; private set; }
    public string RemittanceInfo { get; private set; }
    public RtgsStatus Status { get; private set; }
    public string? BangladeshBankReference { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime InitiatedAtUtc { get; private set; }
    public DateTime? SettledAtUtc { get; private set; }

    // Parameterless constructor for EF Core ORM materialization
    private RtgsTransaction() : base(Guid.Empty) { }

    public RtgsTransaction(
        Guid id,
        string endToEndId,
        RtgsMessageType messageType,
        Money amount,
        RoutingNumber debtorAgentRouting,
        string debtorAccount,
        RoutingNumber creditorAgentRouting,
        string creditorAccount,
        string creditorName,
        string remittanceInfo,
        DateTime initiatedAtUtc) : base(id)
    {
        if (string.IsNullOrWhiteSpace(endToEndId)) throw new ArgumentException("End-to-End ID is required.", nameof(endToEndId));
        if (string.IsNullOrWhiteSpace(debtorAccount)) throw new ArgumentException("Debtor account is required.", nameof(debtorAccount));
        if (string.IsNullOrWhiteSpace(creditorAccount)) throw new ArgumentException("Creditor account is required.", nameof(creditorAccount));
        if (string.IsNullOrWhiteSpace(creditorName)) throw new ArgumentException("Creditor name is required.", nameof(creditorName));
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(debtorAgentRouting, nameof(debtorAgentRouting));
        ArgumentNullException.ThrowIfNull(creditorAgentRouting, nameof(creditorAgentRouting));

        if (amount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidRtgsAmountInvariant",
                "BRPD Circular 12/2024 (1.5.5)",
                $"RTGS transfer amount must be strictly positive (provided: {amount.Amount}).");
        }

        // Regulatory threshold check for customer credit transfers (pacs.008): Minimum 100,000 BDT (BRPD 1.5.5)
        if (messageType == RtgsMessageType.Pacs008_CustomerCreditTransfer && amount < Money.Bdt(100000m))
        {
            throw new InvariantViolationException(
                "RtgsMinimumThresholdInvariant",
                "BRPD Circular 12/2024 (1.5.5)",
                $"Customer RTGS transfer (pacs.008) requires a minimum of 100,000 BDT (provided: {amount.Amount} BDT). Sub-threshold transfers must route via BEFTN or NPSB.");
        }

        EndToEndId = endToEndId.Trim().ToUpperInvariant();
        MessageType = messageType;
        Amount = amount;
        DebtorAgentRouting = debtorAgentRouting;
        DebtorAccount = debtorAccount.Trim();
        CreditorAgentRouting = creditorAgentRouting;
        CreditorAccount = creditorAccount.Trim();
        CreditorName = creditorName.Trim();
        RemittanceInfo = remittanceInfo?.Trim() ?? string.Empty;
        Status = RtgsStatus.Initiated;
        InitiatedAtUtc = initiatedAtUtc;
    }

    public void SettleAtBangladeshBank(string bbReference, DateTime settledAtUtc)
    {
        if (string.IsNullOrWhiteSpace(bbReference)) throw new ArgumentException("Bangladesh Bank reference is required.", nameof(bbReference));

        if (Status == RtgsStatus.SettledAtBangladeshBank)
        {
            throw new InvariantViolationException(
                "RtgsAlreadySettledInvariant",
                "BRPD Circular 12/2024 (1.5.5)",
                $"RTGS transaction '{EndToEndId}' is already settled (BB Ref: {BangladeshBankReference}).");
        }

        if (Status == RtgsStatus.Rejected)
        {
            throw new InvariantViolationException(
                "RejectedRtgsSettlementBlockedInvariant",
                "BRPD Circular 12/2024 (1.5.5)",
                $"Cannot settle rejected RTGS transaction '{EndToEndId}' (Reason: {RejectionReason}).");
        }

        Status = RtgsStatus.SettledAtBangladeshBank;
        BangladeshBankReference = bbReference.Trim();
        SettledAtUtc = settledAtUtc;
    }

    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Rejection reason is required.", nameof(reason));

        if (Status == RtgsStatus.SettledAtBangladeshBank)
        {
            throw new InvariantViolationException(
                "SettledRtgsRejectionProhibitedInvariant",
                "BRPD Circular 12/2024 (1.5.5)",
                $"Cannot reject RTGS transaction '{EndToEndId}' after final central bank settlement.");
        }

        Status = RtgsStatus.Rejected;
        RejectionReason = reason.Trim();
    }
}
