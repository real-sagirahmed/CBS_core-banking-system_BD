using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Deposits;

// ARCHITECTURAL INTENT: CAD Active Lien Lock for Credit Facilities (BRPD Circular 12/2024 Clause 3.8.10 & 3.6.1).
// Deposits encumbered as collateral for Demand Loans Against Deposit (DLAD) or Continuous Overdraft (OD)
// CANNOT be premature encashed, liquidated, or transferred until the loan is fully closed and the lien is explicitly released.
public class DepositLienFacility : AggregateRoot<string>
{
    public string DepositAccountId { get; private set; }
    public string DepositType { get; private set; } // "DPS" or "FDR"
    public string LoanAccountId { get; private set; }
    public Money SanctionedLoanLimit { get; private set; }
    public Money EncumberedAmount { get; private set; }
    public decimal MaximumLtvPercentage { get; private set; }
    public DepositLienStatus Status { get; private set; }
    public DateTime PlacedAtUtc { get; private set; }
    public DateTime? ReleasedAtUtc { get; private set; }
    public string PlacedByOfficerId { get; private set; }
    public string? ReleasedByOfficerId { get; private set; }
    public string ReferenceDocument { get; private set; }
    public string? ReleaseReason { get; private set; }

#pragma warning disable CS8618
    private DepositLienFacility() : base(string.Empty) { }
#pragma warning restore CS8618

    public DepositLienFacility(
        string id,
        string depositAccountId,
        string depositType,
        string loanAccountId,
        Money sanctionedLoanLimit,
        Money encumberedAmount,
        decimal maximumLtvPercentage,
        string placedByOfficerId,
        string referenceDocument) : base(id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Lien ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(depositAccountId)) throw new ArgumentException("Deposit account ID is required.", nameof(depositAccountId));
        if (string.IsNullOrWhiteSpace(loanAccountId)) throw new ArgumentException("Loan account ID is required.", nameof(loanAccountId));
        if (string.IsNullOrWhiteSpace(placedByOfficerId)) throw new ArgumentException("Officer ID is required.", nameof(placedByOfficerId));
        ArgumentNullException.ThrowIfNull(sanctionedLoanLimit);
        ArgumentNullException.ThrowIfNull(encumberedAmount);

        if (maximumLtvPercentage is <= 0 or > 100)
        {
            throw new InvariantViolationException(
                "LtvPercentageInvariant",
                "BRPD Circular 12/2024 (4.1.3, 3.8.10)",
                $"Maximum LTV percentage '{maximumLtvPercentage}%' must be between 1% and 100%.");
        }

        DepositAccountId = depositAccountId;
        DepositType = depositType;
        LoanAccountId = loanAccountId;
        SanctionedLoanLimit = sanctionedLoanLimit;
        EncumberedAmount = encumberedAmount;
        MaximumLtvPercentage = maximumLtvPercentage;
        Status = DepositLienStatus.Active;
        PlacedAtUtc = DateTime.UtcNow;
        PlacedByOfficerId = placedByOfficerId;
        ReferenceDocument = referenceDocument ?? "CAD-SANCTION-ADVICE";
    }

    public void Release(string officerId, string reason)
    {
        if (Status != DepositLienStatus.Active)
        {
            throw new InvariantViolationException(
                "LienStatusInvariant",
                "BRPD Circular 12/2024 (3.8.10)",
                $"Lien '{Id}' cannot be released because it is currently '{Status}'.");
        }

        if (string.IsNullOrWhiteSpace(officerId))
            throw new ArgumentException("Releasing officer ID is required.", nameof(officerId));

        Status = DepositLienStatus.Released;
        ReleasedAtUtc = DateTime.UtcNow;
        ReleasedByOfficerId = officerId;
        ReleaseReason = reason ?? "Loan facility adjusted in full.";
    }
}
