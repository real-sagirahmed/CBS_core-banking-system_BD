using CBS.Domain.Common;
using CBS.Domain.Exceptions;
using CBS.Domain.Enums;

namespace CBS.Domain.Security;

// ARCHITECTURAL INTENT: Administrative Dual-Control Proposal
// Enforces Bangladesh Bank BRPD Circular 12/2024 (10.3.5-x & 1.3.18):
// Dual Control/Authorization (Checker/Maker) shall be ensured for any admin activity in CBS System.
public class AdminActionProposal : AggregateRoot<Guid>
{
    public AdminActionType ActionType { get; private set; }
    public string TargetEntityId { get; private set; }
    public string ProposalPayloadJson { get; private set; }
    public string MakerUserId { get; private set; }
    public DateTime ProposedAtUtc { get; private set; }
    public AdminProposalStatus Status { get; private set; }
    public string? CheckerUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewRemarks { get; private set; }

    #pragma warning disable CS8618
    protected AdminActionProposal() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public AdminActionProposal(
        Guid id,
        AdminActionType actionType,
        string targetEntityId,
        string proposalPayloadJson,
        string makerUserId,
        DateTime proposedAtUtc) : base(id == Guid.Empty ? Guid.NewGuid() : id)
    {
        if (string.IsNullOrWhiteSpace(targetEntityId))
            throw new ArgumentException("Target entity ID is required.", nameof(targetEntityId));
        if (string.IsNullOrWhiteSpace(makerUserId))
            throw new ArgumentException("Maker user ID is required.", nameof(makerUserId));

        ActionType = actionType;
        TargetEntityId = targetEntityId.Trim();
        ProposalPayloadJson = proposalPayloadJson ?? "{}";
        MakerUserId = makerUserId.Trim();
        ProposedAtUtc = proposedAtUtc;
        Status = AdminProposalStatus.PendingApproval;
    }

    public void Approve(string checkerUserId, string remarks, DateTime reviewedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker user ID is required.", nameof(checkerUserId));

        if (Status != AdminProposalStatus.PendingApproval)
        {
            throw new InvariantViolationException(
                "ProposalNotInPendingStateInvariant",
                "BRPD Circular 12/2024 (10.3.5-x)",
                $"Cannot approve proposal in '{Status}' state.");
        }

        // BRPD 10.3.5-x & 1.3.18: Dual Control
        if (checkerUserId.Equals(MakerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (10.3.5-x, 1.3.18)",
                $"Maker '{MakerUserId}' cannot approve their own administrative proposal.");
        }

        CheckerUserId = checkerUserId.Trim();
        ReviewRemarks = remarks?.Trim();
        ReviewedAtUtc = reviewedAtUtc;
        Status = AdminProposalStatus.Approved;
    }

    public void Reject(string checkerUserId, string remarks, DateTime reviewedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker user ID is required.", nameof(checkerUserId));

        if (Status != AdminProposalStatus.PendingApproval)
        {
            throw new InvariantViolationException(
                "ProposalNotInPendingStateInvariant",
                "BRPD Circular 12/2024 (10.3.5-x)",
                $"Cannot reject proposal in '{Status}' state.");
        }

        if (checkerUserId.Equals(MakerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (10.3.5-x, 1.3.18)",
                $"Maker '{MakerUserId}' cannot reject their own administrative proposal.");
        }

        CheckerUserId = checkerUserId.Trim();
        ReviewRemarks = remarks?.Trim();
        ReviewedAtUtc = reviewedAtUtc;
        Status = AdminProposalStatus.Rejected;
    }
}
