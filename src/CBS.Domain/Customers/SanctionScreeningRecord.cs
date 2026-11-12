// ARCHITECTURAL INTENT: Sanction Screening Record Aggregate Root
// Supporting real-time screening against UNSCR 1267/1373, OFAC SDN, EU, and BFIU lists,
// dual-control investigation workflow, and regulatory hard blocking per Bangladesh Bank
// BRPD Circular No. 12 (13 May 2024) Module 8.1.14 and Anti-Money Laundering Act 2012.

using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Customers;

public sealed class SanctionScreeningRecord : AggregateRoot<Guid>
{
    public CifNumber? Cif { get; private set; }
    public string FullName { get; private set; } = null!;
    public DateOnly? DateOfBirth { get; private set; }
    public string? Nationality { get; private set; }
    public SanctionWatchlistType WatchlistType { get; private set; }
    public string? MatchedEntityName { get; private set; }
    public string? MatchedEntityReference { get; private set; }
    public decimal MatchConfidenceScore { get; private set; }
    public SanctionMatchStatus Status { get; private set; }
    public bool IsHardBlocked { get; private set; }
    public string? InvestigatedByUserId { get; private set; }
    public string? InvestigationNotes { get; private set; }
    public DateTime? InvestigatedAtUtc { get; private set; }
    public string? ApprovedByComplianceHeadUserId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public DateTime ScreenedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private SanctionScreeningRecord() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public SanctionScreeningRecord(
        Guid id,
        string fullName,
        DateOnly? dateOfBirth,
        string? nationality,
        CifNumber? cif = null)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required for sanction screening.", nameof(fullName));

        Cif = cif;
        FullName = fullName.Trim().ToUpperInvariant();
        DateOfBirth = dateOfBirth;
        Nationality = nationality?.Trim().ToUpperInvariant();
        WatchlistType = SanctionWatchlistType.Unscr1267;
        Status = SanctionMatchStatus.Clear;
        MatchConfidenceScore = 0m;
        IsHardBlocked = false;
        ScreenedAtUtc = DateTime.UtcNow;
    }

    public void FlagPotentialMatch(
        SanctionWatchlistType watchlistType,
        string matchedEntityName,
        string matchedEntityReference,
        decimal matchConfidenceScore)
    {
        if (string.IsNullOrWhiteSpace(matchedEntityName))
            throw new ArgumentException("Matched entity name is required.", nameof(matchedEntityName));
        if (matchConfidenceScore is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(matchConfidenceScore), "Match score must be between 0 and 100.");

        WatchlistType = watchlistType;
        MatchedEntityName = matchedEntityName.Trim().ToUpperInvariant();
        MatchedEntityReference = matchedEntityReference.Trim().ToUpperInvariant();
        MatchConfidenceScore = matchConfidenceScore;
        Status = SanctionMatchStatus.PotentialMatch;
        // Temporary block pending investigation
        IsHardBlocked = true;
    }

    public void Investigate(
        string complianceOfficerUserId,
        string investigationNotes,
        bool isPotentialTruePositive)
    {
        if (string.IsNullOrWhiteSpace(complianceOfficerUserId))
            throw new ArgumentException("Compliance officer user ID is required.", nameof(complianceOfficerUserId));
        if (string.IsNullOrWhiteSpace(investigationNotes))
            throw new ArgumentException("Investigation notes are mandatory.", nameof(investigationNotes));

        if (Status != SanctionMatchStatus.PotentialMatch)
        {
            throw new InvalidOperationException($"Cannot investigate screening record in '{Status}' status.");
        }

        InvestigatedByUserId = complianceOfficerUserId.Trim();
        InvestigationNotes = investigationNotes.Trim();
        InvestigatedAtUtc = DateTime.UtcNow;
        // Status remains PotentialMatch awaiting Checker approval
    }

    public void ConfirmResolutionByComplianceHead(
        string complianceHeadUserId,
        bool confirmAsSanctioned)
    {
        if (string.IsNullOrWhiteSpace(complianceHeadUserId))
            throw new ArgumentException("Compliance head user ID is required.", nameof(complianceHeadUserId));

        if (string.IsNullOrWhiteSpace(InvestigatedByUserId))
        {
            throw new InvariantViolationException(
                "InvestigationPreconditionInvariant",
                "BRPD Circular 12/2024 (8.1.14)",
                "Sanction potential match must first be investigated by a Compliance Officer (Maker) before Head approval.");
        }

        if (string.Equals(InvestigatedByUserId, complianceHeadUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (8.1.14-b)",
                "Compliance Officer (Maker) cannot approve own sanction investigation resolution. Independent checker required.");
        }

        ApprovedByComplianceHeadUserId = complianceHeadUserId.Trim();
        ApprovedAtUtc = DateTime.UtcNow;

        if (confirmAsSanctioned)
        {
            Status = SanctionMatchStatus.ConfirmedMatch;
            IsHardBlocked = true;
        }
        else
        {
            Status = SanctionMatchStatus.FalsePositive;
            IsHardBlocked = false;
        }
    }
}
