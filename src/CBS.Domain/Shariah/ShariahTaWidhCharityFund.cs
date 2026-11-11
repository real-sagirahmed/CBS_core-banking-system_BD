using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Shariah;

// ARCHITECTURAL INTENT: Under BRPD Circular 12/2024 (4.1.9-iii) and Shariah Rule 4.1:
// Default penalties (Ta'widh) CANNOT be recognized as bank P&L income under any circumstance.
// All accrued default compensation is credited strictly to a Shariah Charity Suspense account
// and subsequently disbursed to central-bank approved social welfare funds upon Shariah Board approval.
public class ShariahTaWidhCharityFund : AggregateRoot<Guid>
{
    public string FacilityReference { get; private set; }
    public string CustomerCif { get; private set; }
    public decimal OverduePenaltyAmount { get; private set; }
    public DateTime AccruedAtUtc { get; private set; }
    public TaWidhStatus Status { get; private set; }
    public string? ApprovedSocialWelfareCause { get; private set; }
    public DateTime? DisbursedAtUtc { get; private set; }
    public string? DisbursedByOfficerId { get; private set; }

    #pragma warning disable CS8618
    protected ShariahTaWidhCharityFund() : base(Guid.NewGuid()) { }
    #pragma warning restore CS8618

    public ShariahTaWidhCharityFund(
        string facilityReference,
        string customerCif,
        decimal overduePenaltyAmount) : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(facilityReference)) throw new ArgumentException("Facility reference required.", nameof(facilityReference));
        if (string.IsNullOrWhiteSpace(customerCif)) throw new ArgumentException("Customer CIF required.", nameof(customerCif));
        if (overduePenaltyAmount <= 0) throw new ArgumentOutOfRangeException(nameof(overduePenaltyAmount), "Penalty amount must be positive.");

        FacilityReference = facilityReference.Trim();
        CustomerCif = customerCif.Trim();
        OverduePenaltyAmount = overduePenaltyAmount;
        AccruedAtUtc = DateTime.UtcNow;
        Status = TaWidhStatus.AccruedInSuspense;
    }

    public void ApproveByShariahBoard(string approvedSocialWelfareCause)
    {
        if (string.IsNullOrWhiteSpace(approvedSocialWelfareCause))
            throw new ArgumentException("Approved social welfare cause required.", nameof(approvedSocialWelfareCause));

        if (Status != TaWidhStatus.AccruedInSuspense)
        {
            throw new InvariantViolationException(
                "InvalidTaWidhStatusInvariant",
                "BRPD 4.1.9-iii",
                $"Cannot approve Ta'widh record in status '{Status}'. Must be '{TaWidhStatus.AccruedInSuspense}'.");
        }

        ApprovedSocialWelfareCause = approvedSocialWelfareCause.Trim();
        Status = TaWidhStatus.ApprovedByShariahBoard;
    }

    public void DisburseToWelfare(string disbursedByOfficerId)
    {
        if (string.IsNullOrWhiteSpace(disbursedByOfficerId))
            throw new ArgumentException("Disbursed by officer ID required.", nameof(disbursedByOfficerId));

        if (Status != TaWidhStatus.ApprovedByShariahBoard)
        {
            throw new InvariantViolationException(
                "UnapprovedCharityDisbursementInvariant",
                "BRPD 4.1.9-iii",
                "Ta'widh charity funds cannot be disbursed without prior Shariah Supervisory Board approval.");
        }

        Status = TaWidhStatus.DisbursedToCharity;
        DisbursedAtUtc = DateTime.UtcNow;
        DisbursedByOfficerId = disbursedByOfficerId.Trim();
    }
}
