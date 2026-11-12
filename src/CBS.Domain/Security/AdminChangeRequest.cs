// ARCHITECTURAL INTENT: Administrative Maker-Checker Dual-Control Change Request aggregate
// enforcing independent checker authorization per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10 (10.3.5-x).

using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Security;

public class AdminChangeRequest : AggregateRoot<string>
{
    public string RequestId => Id;
    public AdminChangeCategory Category { get; private set; }
    public string ChangeDescription { get; private set; } = string.Empty;
    public string ChangePayloadJson { get; private set; } = string.Empty;
    public string MakerUserId { get; private set; } = string.Empty;
    public string MakerBranchCode { get; private set; } = string.Empty;
    public AdminChangeStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string? CheckerUserId { get; private set; }
    public string? CheckerRemarks { get; private set; }
    public DateTime? ActionedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }

    // Concurrency tokens
    public byte[]? RowVersion { get; set; }
    public uint xmin { get; set; }

    // Private constructor for EF Core
    #pragma warning disable CS8618
    private AdminChangeRequest() : base(string.Empty) { }
    #pragma warning restore CS8618

    public AdminChangeRequest(
        string requestId,
        AdminChangeCategory category,
        string changeDescription,
        string changePayloadJson,
        string makerUserId,
        string makerBranchCode,
        DateTime? createdAtUtc = null) : base(requestId?.Trim() ?? string.Empty)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));

        if (string.IsNullOrWhiteSpace(changeDescription))
            throw new ArgumentException("Change description cannot be empty.", nameof(changeDescription));

        if (string.IsNullOrWhiteSpace(makerUserId))
            throw new ArgumentException("Maker User ID cannot be empty.", nameof(makerUserId));

        Category = category;
        ChangeDescription = changeDescription.Trim();
        ChangePayloadJson = string.IsNullOrWhiteSpace(changePayloadJson) ? "{}" : changePayloadJson.Trim();
        MakerUserId = makerUserId.Trim().ToUpperInvariant();
        MakerBranchCode = string.IsNullOrWhiteSpace(makerBranchCode) ? "HO-001" : makerBranchCode.Trim().ToUpperInvariant();
        Status = AdminChangeStatus.PendingCheckerApproval;
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
    }

    public void Approve(string checkerUserId, string? remarks = null, DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker User ID cannot be empty.", nameof(checkerUserId));

        var normalizedChecker = checkerUserId.Trim().ToUpperInvariant();

        // ARCHITECTURAL INTENT: Maker cannot approve their own administrative change request
        if (string.Equals(MakerUserId, normalizedChecker, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "AdminMakerCheckerInvariant",
                "BRPD Circular 12/2024 (10.3.5-x)",
                $"Maker '{MakerUserId}' cannot approve their own administrative change request. Dual-control requires an independent Checker.");
        }

        if (Status != AdminChangeStatus.PendingCheckerApproval)
        {
            throw new InvariantViolationException(
                "AdminChangeStatusInvariant",
                "BRPD Circular 12/2024 (10.3.5-x)",
                $"Cannot approve administrative change request '{RequestId}' because its current status is '{Status}'.");
        }

        Status = AdminChangeStatus.Approved;
        CheckerUserId = normalizedChecker;
        CheckerRemarks = remarks;
        ActionedAtUtc = utcNow ?? DateTime.UtcNow;
    }

    public void Reject(string checkerUserId, string rejectionReason, DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker User ID cannot be empty.", nameof(checkerUserId));

        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException("Rejection reason cannot be empty.", nameof(rejectionReason));

        var normalizedChecker = checkerUserId.Trim().ToUpperInvariant();

        if (string.Equals(MakerUserId, normalizedChecker, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "AdminMakerCheckerInvariant",
                "BRPD Circular 12/2024 (10.3.5-x)",
                $"Maker '{MakerUserId}' cannot reject their own administrative change request. Dual-control requires an independent Checker.");
        }

        if (Status != AdminChangeStatus.PendingCheckerApproval)
        {
            throw new InvariantViolationException(
                "AdminChangeStatusInvariant",
                "BRPD Circular 12/2024 (10.3.5-x)",
                $"Cannot reject administrative change request '{RequestId}' because its current status is '{Status}'.");
        }

        Status = AdminChangeStatus.Rejected;
        CheckerUserId = normalizedChecker;
        RejectionReason = rejectionReason.Trim();
        ActionedAtUtc = utcNow ?? DateTime.UtcNow;
    }
}
