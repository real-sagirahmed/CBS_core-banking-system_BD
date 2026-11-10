using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.TradeFinance;

/// <summary>
/// Import Shipping Guarantee (SG) Aggregate Root conforming to Bangladesh Bank BRPD Circular 12, Clause 5.1.2.
/// Allows customs clearance before original Bill of Lading arrival.
/// Invariants:
/// 1. Mandatorily requires 100% Cash Margin equal to Guarantee Amount (BRPD 5.1.2-iii).
/// 2. Tagged against an active Import LC (BRPD 5.1.2-ii).
/// 3. Release/Cancellation strictly requires surrender of original Bill of Lading (BRPD 5.1.2-vii).
/// 4. Dual control separation of duty (Maker != Checker).
/// </summary>
public sealed class ShippingGuarantee : AggregateRoot<string>
{
    public string SgReference => Id;
    public string BranchCode { get; private set; } = null!;
    public string? CustomerDepositAccountNumber { get; private set; }
    public string LcNumber { get; private set; } = null!;
    public Guid ApplicantId { get; private set; }
    public string CarrierName { get; private set; } = null!;
    public string VesselName { get; private set; } = null!;
    public string PortOfDischarge { get; private set; } = null!;
    public Money GuaranteeAmount { get; private set; } = null!;
    public Money CashMarginAmount { get; private set; } = null!;
    public ShippingGuaranteeStatus Status { get; private set; }
    public string? SurrenderedOriginalBlNumber { get; private set; }
    public DateTime? OriginalBlSurrenderedAtUtc { get; private set; }
    public string MakerUserId { get; private set; } = null!;
    public string? CheckerUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? AuthorizedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private ShippingGuarantee() : base(string.Empty) { }
    #pragma warning restore CS8618

    private ShippingGuarantee(string sgReference) : base(sgReference) { }

    public static ShippingGuarantee Issue(
        string sgReference,
        string lcNumber,
        Guid applicantId,
        string carrierName,
        string vesselName,
        string portOfDischarge,
        Money guaranteeAmount,
        Money cashMarginAmount,
        string makerUserId,
        string branchCode = "001",
        string? customerDepositAccountNumber = null)
    {
        if (string.IsNullOrWhiteSpace(sgReference))
            throw new ArgumentException("SG Reference is required.", nameof(sgReference));
        if (string.IsNullOrWhiteSpace(lcNumber))
            throw new ArgumentException("Tagged LC Number is required.", nameof(lcNumber));
        if (string.IsNullOrWhiteSpace(carrierName))
            throw new ArgumentException("Carrier/Shipping line name is required.", nameof(carrierName));
        if (string.IsNullOrWhiteSpace(vesselName))
            throw new ArgumentException("Vessel name is required.", nameof(vesselName));
        if (string.IsNullOrWhiteSpace(makerUserId))
            throw new ArgumentException("Maker User ID is required.", nameof(makerUserId));

        // BRPD 5.1.2-iii: Mandatory 100% Cash Margin lock for Shipping Guarantee
        if (cashMarginAmount.Currency != guaranteeAmount.Currency)
            throw new InvalidOperationException("Cash margin currency must match Shipping Guarantee currency.");

        if (cashMarginAmount.Amount < guaranteeAmount.Amount)
            throw new InvalidOperationException($"Regulatory Invariant Violation [BRPD 5.1.2-iii]: Shipping Guarantee strictly requires 100% cash margin. Margin provided ({cashMarginAmount.Amount}) is less than Guarantee amount ({guaranteeAmount.Amount}).");

        return new ShippingGuarantee(sgReference.Trim())
        {
            LcNumber = lcNumber.Trim(),
            ApplicantId = applicantId,
            CarrierName = carrierName.Trim(),
            VesselName = vesselName.Trim(),
            PortOfDischarge = portOfDischarge.Trim(),
            GuaranteeAmount = guaranteeAmount,
            CashMarginAmount = cashMarginAmount,
            Status = ShippingGuaranteeStatus.Active,
            MakerUserId = makerUserId.Trim(),
            BranchCode = string.IsNullOrWhiteSpace(branchCode) ? "001" : branchCode.Trim(),
            CustomerDepositAccountNumber = customerDepositAccountNumber?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Authorize(string checkerUserId)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));

        if (string.Equals(MakerUserId, checkerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 1.3.18, 10.3.5-x]: Dual-control separation of duty enforced. The maker cannot authorize their own Shipping Guarantee.");

        CheckerUserId = checkerUserId.Trim();
        AuthorizedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Releases the shipping guarantee upon physical receipt and surrender of the original Bill of Lading (BL).
    /// Reference: BRPD Circular 12, 5.1.2-vii ("System shall remove SG after arrival of original documents").
    /// </summary>
    public void ReleaseUponOriginalBlSurrender(string originalBlNumber, string releasedByUserId)
    {
        if (Status != ShippingGuaranteeStatus.Active)
            throw new InvalidOperationException($"Cannot release Shipping Guarantee in status '{Status}'. Must be Active.");

        if (string.IsNullOrWhiteSpace(originalBlNumber))
            throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 5.1.2-vii]: Original Bill of Lading (BL) number is required to release Shipping Guarantee.");

        if (string.IsNullOrWhiteSpace(releasedByUserId))
            throw new ArgumentException("Releasing user ID is required.", nameof(releasedByUserId));

        SurrenderedOriginalBlNumber = originalBlNumber.Trim();
        OriginalBlSurrenderedAtUtc = DateTime.UtcNow;
        Status = ShippingGuaranteeStatus.ReleasedUponBlSurrender;
    }
}
