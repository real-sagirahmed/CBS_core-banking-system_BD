// ARCHITECTURAL INTENT: Supervisory EOD Back-Value Audit Journal Entry aggregate
// enabling independent audit inspection of back-value entries per BRPD Circular No. 12 (13 May 2024) Module 1 (1.3.4-iii).

using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;

namespace CBS.Domain.BackValue;

public class BackValueAuditJournalEntry : AggregateRoot<string>
{
    public string EntryId => Id;
    public string RequestId { get; private set; } = string.Empty;
    public string BranchCode { get; private set; } = string.Empty;
    public DateOnly BookingDate { get; private set; }
    public DateOnly ValueDate { get; private set; }
    public int DaysVariance { get; private set; }
    public string TargetAccountOrGl { get; private set; } = string.Empty;
    public decimal PrincipalAmount { get; private set; }
    public string Currency { get; private set; } = "BDT";
    public decimal RetroactiveInterestAmount { get; private set; }
    public AdjustmentDirection Direction { get; private set; }
    public string MakerUserId { get; private set; } = string.Empty;
    public string CheckerUserId { get; private set; } = string.Empty;
    public DateTime ApprovalTimeUtc { get; private set; }
    public string VoucherNumber { get; private set; } = string.Empty;
    public string AuditNarration { get; private set; } = string.Empty;

    // Concurrency tokens
    public byte[]? RowVersion { get; set; }
    public uint xmin { get; set; }

    // Private constructor for EF Core
    #pragma warning disable CS8618
    private BackValueAuditJournalEntry() : base(string.Empty) { }
    #pragma warning restore CS8618

    public BackValueAuditJournalEntry(
        string entryId,
        string requestId,
        string branchCode,
        DateOnly bookingDate,
        DateOnly valueDate,
        int daysVariance,
        string targetAccountOrGl,
        decimal principalAmount,
        string currency,
        decimal retroactiveInterestAmount,
        AdjustmentDirection direction,
        string makerUserId,
        string checkerUserId,
        DateTime approvalTimeUtc,
        string voucherNumber,
        string auditNarration) : base(entryId?.Trim() ?? string.Empty)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            throw new ArgumentException("Entry ID cannot be empty.", nameof(entryId));

        EntryIdPropAssign(
            requestId,
            branchCode,
            bookingDate,
            valueDate,
            daysVariance,
            targetAccountOrGl,
            principalAmount,
            currency,
            retroactiveInterestAmount,
            direction,
            makerUserId,
            checkerUserId,
            approvalTimeUtc,
            voucherNumber,
            auditNarration);
    }

    private void EntryIdPropAssign(
        string requestId,
        string branchCode,
        DateOnly bookingDate,
        DateOnly valueDate,
        int daysVariance,
        string targetAccountOrGl,
        decimal principalAmount,
        string currency,
        decimal retroactiveInterestAmount,
        AdjustmentDirection direction,
        string makerUserId,
        string checkerUserId,
        DateTime approvalTimeUtc,
        string voucherNumber,
        string auditNarration)
    {
        RequestId = requestId.Trim();
        BranchCode = string.IsNullOrWhiteSpace(branchCode) ? "BR-001" : branchCode.Trim().ToUpperInvariant();
        BookingDate = bookingDate;
        ValueDate = valueDate;
        DaysVariance = daysVariance;
        TargetAccountOrGl = targetAccountOrGl.Trim().ToUpperInvariant();
        PrincipalAmount = principalAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "BDT" : currency.Trim().ToUpperInvariant();
        RetroactiveInterestAmount = retroactiveInterestAmount;
        Direction = direction;
        MakerUserId = makerUserId.Trim().ToUpperInvariant();
        CheckerUserId = checkerUserId.Trim().ToUpperInvariant();
        ApprovalTimeUtc = approvalTimeUtc;
        VoucherNumber = voucherNumber.Trim();
        AuditNarration = auditNarration.Trim();
    }
}
