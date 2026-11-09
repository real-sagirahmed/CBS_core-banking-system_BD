using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Treasury;

/// <summary>
/// Treasury Deal Aggregate Root conforming to Bangladesh Bank BRPD Circular 12 (Module 6: Treasury).
/// Invariants:
/// 1. Three-tier segregation of duties: Front Office captures -> Mid Office verifies limits -> Back Office settles (BRPD 6.5.1-ii).
/// 2. FrontOfficeDealer != MidOfficeOfficer != BackOfficeSettlementOfficer (BRPD 6.5.4, 10.3.5-x).
/// 3. Deal cannot be settled in back office without mid-office verification sign-off (BRPD 6.5.1-ii).
/// 4. Value Date cannot be earlier than Deal Date.
/// </summary>
public sealed class TreasuryDeal : AggregateRoot<string>
{
    public string DealReference => Id;
    public TreasuryDealType Type { get; private set; }
    public DealDirection Direction { get; private set; }
    public string CounterpartyBankBic { get; private set; } = null!;
    public string CounterpartyName { get; private set; } = null!;
    public Money PrimaryAmount { get; private set; } = null!;
    public Money SecondaryAmount { get; private set; } = null!;
    public decimal ExchangeRate { get; private set; }
    public decimal InterestRate { get; private set; }
    public DateTime DealDate { get; private set; }
    public DateTime ValueDate { get; private set; }
    public TreasuryDealStatus Status { get; private set; }

    public string FrontOfficeDealerUserId { get; private set; } = null!;
    public string? MidOfficeRiskOfficerUserId { get; private set; }
    public DateTime? MidOfficeVerifiedAtUtc { get; private set; }
    public string? BackOfficeSettlementUserId { get; private set; }
    public DateTime? SettledAtUtc { get; private set; }

    private TreasuryDeal(string dealReference) : base(dealReference) { }

    public static TreasuryDeal CaptureByFrontOffice(
        string dealReference,
        TreasuryDealType type,
        DealDirection direction,
        string counterpartyBankBic,
        string counterpartyName,
        Money primaryAmount,
        Money secondaryAmount,
        decimal exchangeRate,
        decimal interestRate,
        DateTime dealDate,
        DateTime valueDate,
        string frontOfficeDealerUserId)
    {
        if (string.IsNullOrWhiteSpace(dealReference))
            throw new ArgumentException("Deal reference is required.", nameof(dealReference));
        if (string.IsNullOrWhiteSpace(counterpartyBankBic))
            throw new ArgumentException("Counterparty BIC is required.", nameof(counterpartyBankBic));
        if (string.IsNullOrWhiteSpace(counterpartyName))
            throw new ArgumentException("Counterparty name is required.", nameof(counterpartyName));
        if (string.IsNullOrWhiteSpace(frontOfficeDealerUserId))
            throw new ArgumentException("Front office dealer user ID is required.", nameof(frontOfficeDealerUserId));

        if (primaryAmount.Amount <= 0m || secondaryAmount.Amount <= 0m)
            throw new InvalidOperationException("Deal monetary amounts must be strictly positive.");

        if (valueDate.Date < dealDate.Date)
            throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 6.5.1]: Value date cannot be earlier than deal trade date.");

        return new TreasuryDeal(dealReference.Trim())
        {
            Type = type,
            Direction = direction,
            CounterpartyBankBic = counterpartyBankBic.Trim().ToUpperInvariant(),
            CounterpartyName = counterpartyName.Trim(),
            PrimaryAmount = primaryAmount,
            SecondaryAmount = secondaryAmount,
            ExchangeRate = exchangeRate,
            InterestRate = interestRate,
            DealDate = dealDate.Date,
            ValueDate = valueDate.Date,
            Status = TreasuryDealStatus.CapturedByFrontOffice,
            FrontOfficeDealerUserId = frontOfficeDealerUserId.Trim()
        };
    }

    /// <summary>
    /// Mid-Office limit, counterparty exposure, and market risk verification.
    /// Invariant: MidOfficeOfficer != FrontOfficeDealer (BRPD 6.5.1-ii, 10.3.5-x).
    /// </summary>
    public void VerifyByMidOffice(string midOfficeOfficerUserId)
    {
        if (Status != TreasuryDealStatus.CapturedByFrontOffice)
            throw new InvalidOperationException($"Cannot perform mid-office verification for deal in status '{Status}'. Must be CapturedByFrontOffice.");

        if (string.IsNullOrWhiteSpace(midOfficeOfficerUserId))
            throw new ArgumentException("Mid office officer user ID is required.", nameof(midOfficeOfficerUserId));

        if (string.Equals(FrontOfficeDealerUserId, midOfficeOfficerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 6.5.1-ii, 10.3.5-x]: Front-office dealer cannot perform mid-office risk and limit verification.");

        MidOfficeRiskOfficerUserId = midOfficeOfficerUserId.Trim();
        MidOfficeVerifiedAtUtc = DateTime.UtcNow;
        Status = TreasuryDealStatus.VerifiedByMidOffice;
    }

    /// <summary>
    /// Back-Office final settlement execution and payment release.
    /// Invariant: BackOfficeOfficer != FrontOfficeDealer AND BackOfficeOfficer != MidOfficeOfficer (BRPD 6.5.1-ii, 6.5.4).
    /// </summary>
    public void SettleByBackOffice(string backOfficeOfficerUserId)
    {
        if (Status != TreasuryDealStatus.VerifiedByMidOffice)
            throw new InvalidOperationException($"Regulatory Invariant Violation [BRPD 6.5.1-ii, 6.5.4-ii]: Settlement is prohibited before mid-office risk and limit verification. Current status: '{Status}'.");

        if (string.IsNullOrWhiteSpace(backOfficeOfficerUserId))
            throw new ArgumentException("Back office settlement user ID is required.", nameof(backOfficeOfficerUserId));

        var backOfficeUser = backOfficeOfficerUserId.Trim();

        if (string.Equals(FrontOfficeDealerUserId, backOfficeUser, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 6.5.1-ii, 6.5.4-i]: Front-office dealer strictly prohibited from executing back-office settlement.");

        if (string.Equals(MidOfficeRiskOfficerUserId, backOfficeUser, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 6.5.1-ii, 6.5.4-i]: Mid-office risk officer strictly prohibited from executing back-office settlement.");

        BackOfficeSettlementUserId = backOfficeUser;
        SettledAtUtc = DateTime.UtcNow;
        Status = TreasuryDealStatus.SettledByBackOffice;
    }
}
