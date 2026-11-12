// ARCHITECTURAL INTENT: Treasury Limit Profile Aggregate Root
// Enforcing Dealer Stop-Loss, Daylight, Overnight, Counterparty Credit Lines, and Central Bank Net Open Position (NOP) limits
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 6.2.2-iii, 6.2.2-iv, 6.2.2-v, 6.3.1-e, 6.3.1-g, 6.3.1-n.

using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Treasury;

public sealed class TreasuryLimitProfile : AggregateRoot<Guid>
{
    public TreasuryLimitType LimitType { get; private set; }
    public string Identifier { get; private set; } = null!; // DealerUserId, CounterpartyBic, or "BANK_NOP_AGGREGATE"
    public string Currency { get; private set; } = null!;
    public decimal SanctionedLimit { get; private set; }
    public decimal UtilizedAmount { get; private set; }
    public decimal AvailableAmount => Math.Max(0m, SanctionedLimit - UtilizedAmount);
    public bool IsActive { get; private set; }
    public string ApprovedByUserId { get; private set; } = null!;
    public DateOnly EffectiveDate { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private TreasuryLimitProfile() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public TreasuryLimitProfile(
        Guid id,
        TreasuryLimitType limitType,
        string identifier,
        string currency,
        decimal sanctionedLimit,
        string approvedByUserId,
        DateOnly effectiveDate,
        DateOnly? expiryDate = null)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Limit identifier is required.", nameof(identifier));
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.", nameof(currency));
        if (string.IsNullOrWhiteSpace(approvedByUserId))
            throw new ArgumentException("Approved by User ID is required.", nameof(approvedByUserId));
        if (sanctionedLimit <= 0)
        {
            throw new InvariantViolationException(
                "InvalidTreasuryLimitAmountInvariant",
                "BRPD Circular 12/2024 (6.3.1)",
                $"Sanctioned treasury limit must be strictly positive ({sanctionedLimit}).");
        }

        LimitType = limitType;
        Identifier = identifier.Trim().ToUpperInvariant();
        Currency = currency.Trim().ToUpperInvariant();
        SanctionedLimit = sanctionedLimit;
        UtilizedAmount = 0m;
        IsActive = true;
        ApprovedByUserId = approvedByUserId.Trim();
        EffectiveDate = effectiveDate;
        ExpiryDate = expiryDate;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void CheckAndUtilize(decimal amount, string dealReference)
    {
        if (amount <= 0)
            throw new ArgumentException("Utilization amount must be positive.", nameof(amount));

        if (!IsActive)
        {
            throw new InvariantViolationException(
                "InactiveTreasuryLimitInvariant",
                "BRPD Circular 12/2024 (6.3.1)",
                $"Treasury limit profile '{Identifier}' ({LimitType}) is inactive.");
        }

        if (UtilizedAmount + amount > SanctionedLimit)
        {
            throw new InvariantViolationException(
                "TreasuryLimitBreachedInvariant",
                "BRPD Circular 12/2024 (6.2.2-iii, 6.3.1-e, 6.3.1-n)",
                $"Treasury deal '{dealReference}' requires {amount:N2} {Currency}, which breaches sanctioned limit of {SanctionedLimit:N2} {Currency} (Current utilization: {UtilizedAmount:N2}, Available: {AvailableAmount:N2}).");
        }

        UtilizedAmount += amount;
    }

    public void Release(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Release amount must be positive.", nameof(amount));

        UtilizedAmount = Math.Max(0m, UtilizedAmount - amount);
    }

    public void ResetUtilization()
    {
        // For daylight/stop-loss limits reset at Beginning of Day (BOD)
        UtilizedAmount = 0m;
    }

    public void UpdateLimit(decimal newLimit, string approvedByUserId)
    {
        if (newLimit <= 0)
            throw new ArgumentException("New limit must be positive.", nameof(newLimit));
        if (string.IsNullOrWhiteSpace(approvedByUserId))
            throw new ArgumentException("Approved by User ID is required.", nameof(approvedByUserId));

        SanctionedLimit = newLimit;
        ApprovedByUserId = approvedByUserId.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
