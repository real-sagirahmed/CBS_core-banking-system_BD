using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.GeneralLedger;

// ARCHITECTURAL INTENT: End-of-Day (EOD) and Beginning-of-Day (BOD) state engine.
// Hard regulatory lock: Day close is prohibited if any unauthorized/pending vouchers exist (BRPD Circular 12/2024 Section 1.2.1, 1.3.15-i).
public class BranchOperatingDay : AggregateRoot<string> // ID is BranchCode
{
    public DateOnly CurrentBusinessDate { get; private set; }
    public DayStatus Status { get; private set; }
    public DateTime? DayOpenedAtUtc { get; private set; }
    public DateTime? DayClosedAtUtc { get; private set; }
    public string? ClosedByUserId { get; private set; }

    // Parameterless constructor for EF Core ORM materialization
    private BranchOperatingDay() : base(string.Empty) { }

    public BranchOperatingDay(string branchCode, DateOnly initialBusinessDate) : base(branchCode)
    {
        if (string.IsNullOrWhiteSpace(branchCode)) throw new ArgumentException("Branch code required.", nameof(branchCode));

        CurrentBusinessDate = initialBusinessDate;
        Status = DayStatus.Open;
        DayOpenedAtUtc = DateTime.UtcNow;
    }

    public void InitiateEodBatch(int pendingVouchersCount)
    {
        if (Status != DayStatus.Open)
        {
            throw new InvalidOperationException($"Cannot initiate EOD batch for branch '{Id}' in status '{Status}'. Must be 'Open'.");
        }

        // HARD REGULATORY GATE: BRPD 1.3.15-i
        if (pendingVouchersCount > 0)
        {
            throw new InvariantViolationException(
                "PendingVoucherDayCloseBlockedInvariant",
                "BRPD Circular 12/2024 (1.3.15-i, 1.2.1)",
                $"Branch '{Id}' has {pendingVouchersCount} unauthorized/pending voucher(s). Day close is strictly prohibited until all vouchers are authorized or canceled.");
        }

        Status = DayStatus.EodBatchRunning;
    }

    public void CompleteDayClose(string closedByUserId)
    {
        if (Status != DayStatus.EodBatchRunning)
        {
            throw new InvalidOperationException($"Cannot complete day close in status '{Status}'. Must be 'EodBatchRunning'.");
        }

        if (string.IsNullOrWhiteSpace(closedByUserId))
            throw new ArgumentException("Closed by User ID required.", nameof(closedByUserId));

        Status = DayStatus.Closed;
        DayClosedAtUtc = DateTime.UtcNow;
        ClosedByUserId = closedByUserId.Trim();
    }

    public void ExecuteBeginningOfDay(DateOnly nextBusinessDate)
    {
        if (Status != DayStatus.Closed)
        {
            throw new InvalidOperationException($"Cannot execute BOD in status '{Status}'. Branch must be 'Closed'.");
        }

        if (nextBusinessDate <= CurrentBusinessDate)
        {
            throw new InvalidOperationException($"Next business date ({nextBusinessDate}) must be greater than current business date ({CurrentBusinessDate}).");
        }

        CurrentBusinessDate = nextBusinessDate;
        Status = DayStatus.Open;
        DayOpenedAtUtc = DateTime.UtcNow;
        DayClosedAtUtc = null;
        ClosedByUserId = null;
    }
}
