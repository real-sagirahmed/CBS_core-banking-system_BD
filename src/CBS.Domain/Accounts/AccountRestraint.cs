using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Accounts;

// ARCHITECTURAL INTENT: Account Restraints, Court Attachments, NBR Garnishee, BFIU Directives, and Liens
// strictly enforcing Bangladesh Bank BRPD Circular No. 12/2024 Modules 1.6.2-v, vi, 1.6.6 & 3.1.15.
// Dual-control maker-checker segregation is mandatory on creation, release, and realization.
public class AccountRestraint : Entity<string>
{
    public AccountNumber AccountNumber { get; private set; }
    public RestraintType RestraintType { get; private set; }
    public RestraintScope Scope { get; private set; }
    public Money HoldAmount { get; private set; }
    public string ReasonCategory { get; private set; }
    public string NarrativeReason { get; private set; }
    public string IssuingAuthority { get; private set; }
    public string ReferenceCaseNo { get; private set; }
    public string PlacedByUserId { get; private set; }
    public string AuthorizedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public RestraintStatus Status { get; private set; }
    public string? ReleasedByUserId { get; private set; }
    public string? ReleaseAuthorizedByUserId { get; private set; }
    public DateTime? ReleasedAtUtc { get; private set; }
    public string? ReleaseRemarks { get; private set; }
    public Money? ForeclosedAmount { get; private set; }
    public string? ForeclosedSettlementGl { get; private set; }
    public DateTime? ForeclosedAtUtc { get; private set; }

    #pragma warning disable CS8618
    protected AccountRestraint() : base(string.Empty) { }
    #pragma warning restore CS8618

    public AccountRestraint(
        string id,
        AccountNumber accountNumber,
        RestraintType restraintType,
        RestraintScope scope,
        Money holdAmount,
        string reasonCategory,
        string narrativeReason,
        string issuingAuthority,
        string referenceCaseNo,
        string placedByUserId,
        string authorizedByUserId,
        DateTime createdAtUtc,
        DateOnly? expiryDate = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Restraint ID is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(accountNumber, nameof(accountNumber));
        ArgumentNullException.ThrowIfNull(holdAmount, nameof(holdAmount));

        if (string.IsNullOrWhiteSpace(reasonCategory)) throw new ArgumentException("Reason category is required (BRPD 3.1.15-xi).", nameof(reasonCategory));
        if (string.IsNullOrWhiteSpace(narrativeReason)) throw new ArgumentException("Narrative reason is required.", nameof(narrativeReason));
        if (string.IsNullOrWhiteSpace(issuingAuthority)) throw new ArgumentException("Issuing authority is required.", nameof(issuingAuthority));
        if (string.IsNullOrWhiteSpace(referenceCaseNo)) throw new ArgumentException("Reference case/instruction number is required.", nameof(referenceCaseNo));
        if (string.IsNullOrWhiteSpace(placedByUserId)) throw new ArgumentException("Maker User ID is required.", nameof(placedByUserId));
        if (string.IsNullOrWhiteSpace(authorizedByUserId)) throw new ArgumentException("Checker User ID is required.", nameof(authorizedByUserId));

        // HARD LOCK: Maker-Checker separation (BRPD 1.3.18, 10.3.5-x)
        if (placedByUserId.Trim().Equals(authorizedByUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Maker '{placedByUserId}' cannot authorize their own account restraint. Strict dual-control duty segregation required.");
        }

        if (scope == RestraintScope.PartialAmountHold && holdAmount.Amount <= 0m)
        {
            throw new InvariantViolationException(
                "InvalidHoldAmountInvariant",
                "BRPD Circular 12/2024 (3.1.15-ix)",
                "PartialAmountHold must specify a strictly positive hold amount.");
        }

        AccountNumber = accountNumber;
        RestraintType = restraintType;
        Scope = scope;
        HoldAmount = holdAmount;
        ReasonCategory = reasonCategory.Trim();
        NarrativeReason = narrativeReason.Trim();
        IssuingAuthority = issuingAuthority.Trim();
        ReferenceCaseNo = referenceCaseNo.Trim();
        PlacedByUserId = placedByUserId.Trim();
        AuthorizedByUserId = authorizedByUserId.Trim();
        CreatedAtUtc = createdAtUtc;
        ExpiryDate = expiryDate;
        Status = RestraintStatus.Active;
    }

    public void Release(string makerUserId, string checkerUserId, string releaseRemarks, DateTime releasedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(makerUserId)) throw new ArgumentException("Maker User ID is required.", nameof(makerUserId));
        if (string.IsNullOrWhiteSpace(checkerUserId)) throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));
        if (string.IsNullOrWhiteSpace(releaseRemarks)) throw new ArgumentException("Release remarks/legal clearance reference is required.", nameof(releaseRemarks));

        if (Status != RestraintStatus.Active)
        {
            throw new InvalidOperationException($"Cannot release restraint '{Id}' with status '{Status}'. Only Active restraints can be released.");
        }

        if (makerUserId.Trim().Equals(checkerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Maker '{makerUserId}' cannot authorize their own restraint release request.");
        }

        ReleasedByUserId = makerUserId.Trim();
        ReleaseAuthorizedByUserId = checkerUserId.Trim();
        ReleaseRemarks = releaseRemarks.Trim();
        ReleasedAtUtc = releasedAtUtc;
        Status = RestraintStatus.Released;
    }

    public void ForecloseRealize(string checkerUserId, Money realizeAmount, string settlementGl, string remarks, DateTime foreclosedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId)) throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));
        if (string.IsNullOrWhiteSpace(settlementGl)) throw new ArgumentException("Settlement GL account is required.", nameof(settlementGl));
        ArgumentNullException.ThrowIfNull(realizeAmount, nameof(realizeAmount));

        if (Status != RestraintStatus.Active)
        {
            throw new InvalidOperationException($"Cannot realize restraint '{Id}' with status '{Status}'. Only Active restraints can be realized.");
        }

        if (Scope == RestraintScope.PartialAmountHold && realizeAmount > HoldAmount)
        {
            throw new InvariantViolationException(
                "ExceededHoldRealizationInvariant",
                "BRPD Circular 12/2024 (1.6.6-ii, 3.1.15-ix)",
                $"Realization amount '{realizeAmount}' cannot exceed active hold amount '{HoldAmount}'.");
        }

        ForeclosedAmount = realizeAmount;
        ForeclosedSettlementGl = settlementGl.Trim();
        ForeclosedAtUtc = foreclosedAtUtc;
        Status = RestraintStatus.ForeclosedRealized;
    }
}
