using CBS.Domain.Common;
using CBS.Domain.Exceptions;
using CBS.Domain.Enums;

namespace CBS.Domain.Security;

// ARCHITECTURAL INTENT: Transaction DOA Authorization Override
// Enforces Bangladesh Bank BRPD Circular 12/2024 (10.2.3 & 10.3.6-iii):
// Tracks higher-tier supervisor authorization overrides for transactions exceeding maker limits.
public class TransactionDoaOverride : AggregateRoot<Guid>
{
    public string TransactionReference { get; private set; }
    public string AccountNumber { get; private set; }
    public string InitiatorUserId { get; private set; }
    public decimal TransactionAmount { get; private set; }
    public string TransactionType { get; private set; }
    public decimal UserLimit { get; private set; }
    public OverrideStatus Status { get; private set; }
    public string? SupervisorUserId { get; private set; }
    public DateTime? AuthorizedAtUtc { get; private set; }
    public string? SupervisorRemarks { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    protected TransactionDoaOverride() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public TransactionDoaOverride(
        Guid id,
        string transactionReference,
        string accountNumber,
        string initiatorUserId,
        decimal transactionAmount,
        string transactionType,
        decimal userLimit,
        DateTime createdAtUtc) : base(id == Guid.Empty ? Guid.NewGuid() : id)
    {
        if (string.IsNullOrWhiteSpace(transactionReference))
            throw new ArgumentException("Transaction reference is required.", nameof(transactionReference));
        if (string.IsNullOrWhiteSpace(initiatorUserId))
            throw new ArgumentException("Initiator user ID is required.", nameof(initiatorUserId));

        TransactionReference = transactionReference.Trim();
        AccountNumber = accountNumber.Trim();
        InitiatorUserId = initiatorUserId.Trim();
        TransactionAmount = transactionAmount;
        TransactionType = transactionType.Trim();
        UserLimit = userLimit;
        Status = OverrideStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public void Authorize(string supervisorUserId, string remarks, DateTime authorizedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            throw new ArgumentException("Supervisor user ID is required.", nameof(supervisorUserId));

        if (Status != OverrideStatus.Pending)
        {
            throw new InvariantViolationException(
                "OverrideNotInPendingStateInvariant",
                "BRPD Circular 12/2024 (10.2.3)",
                $"Cannot authorize override in '{Status}' state.");
        }

        // BRPD 10.2.3: Dual control on transaction limit override
        if (supervisorUserId.Equals(InitiatorUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (10.2.3, 1.3.18)",
                $"Initiator '{InitiatorUserId}' cannot authorize their own transaction limit override.");
        }

        SupervisorUserId = supervisorUserId.Trim();
        SupervisorRemarks = remarks?.Trim();
        AuthorizedAtUtc = authorizedAtUtc;
        Status = OverrideStatus.Authorized;
    }

    public void Decline(string supervisorUserId, string remarks, DateTime declinedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            throw new ArgumentException("Supervisor user ID is required.", nameof(supervisorUserId));

        if (Status != OverrideStatus.Pending)
        {
            throw new InvariantViolationException(
                "OverrideNotInPendingStateInvariant",
                "BRPD Circular 12/2024 (10.2.3)",
                $"Cannot decline override in '{Status}' state.");
        }

        SupervisorUserId = supervisorUserId.Trim();
        SupervisorRemarks = remarks?.Trim();
        AuthorizedAtUtc = declinedAtUtc;
        Status = OverrideStatus.Declined;
    }
}
