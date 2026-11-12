using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Lending;

// ARCHITECTURAL INTENT: Qualitative Judgment (QJ) Inspection Override Record Aggregate Root
// tracking auditor/credit inspection downgrades, justification remarks, dual-control approvals,
// and EOD auto-upgrade lock status per Bangladesh Bank BRPD Circular 12 (4.1.11-iii, 4.2.27-iii, Rule 5.2).
public sealed class QualitativeJudgmentRecord : AggregateRoot<Guid>
{
    public AccountNumber LoanAccountNumber { get; private set; } = null!;
    public LoanClassificationGrade OriginalClassification { get; private set; }
    public LoanClassificationGrade OverriddenClassification { get; private set; }
    public QualitativeJudgmentReason Reason { get; private set; }
    public string JustificationRemarks { get; private set; } = null!;
    public string InspectorUserId { get; private set; } = null!;
    public string CheckerUserId { get; private set; } = null!;
    public DateOnly EffectiveDate { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevocationRemarks { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private QualitativeJudgmentRecord() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public QualitativeJudgmentRecord(
        Guid id,
        AccountNumber loanAccountNumber,
        LoanClassificationGrade originalClassification,
        LoanClassificationGrade overriddenClassification,
        QualitativeJudgmentReason reason,
        string justificationRemarks,
        string inspectorUserId,
        string checkerUserId,
        DateOnly effectiveDate)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(loanAccountNumber, nameof(loanAccountNumber));
        if (string.IsNullOrWhiteSpace(justificationRemarks))
            throw new ArgumentException("Justification remarks are required.", nameof(justificationRemarks));
        if (string.IsNullOrWhiteSpace(inspectorUserId))
            throw new ArgumentException("Inspector user ID is required.", nameof(inspectorUserId));
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker user ID is required.", nameof(checkerUserId));

        if (string.Equals(inspectorUserId.Trim(), checkerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                "Inspector cannot act as Checker to authorize Qualitative Judgment override.");
        }

        LoanAccountNumber = loanAccountNumber;
        OriginalClassification = originalClassification;
        OverriddenClassification = overriddenClassification;
        Reason = reason;
        JustificationRemarks = justificationRemarks.Trim();
        InspectorUserId = inspectorUserId.Trim();
        CheckerUserId = checkerUserId.Trim();
        EffectiveDate = effectiveDate;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Revoke(string revocationRemarks, string revokingUserId)
    {
        if (string.IsNullOrWhiteSpace(revocationRemarks))
            throw new ArgumentException("Revocation justification remarks are required.", nameof(revocationRemarks));
        if (string.IsNullOrWhiteSpace(revokingUserId))
            throw new ArgumentException("Revoking user ID is required.", nameof(revokingUserId));

        IsActive = false;
        RevokedAtUtc = DateTime.UtcNow;
        RevocationRemarks = revocationRemarks.Trim();
    }
}
