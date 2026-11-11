using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.StandingInstructions;

// ARCHITECTURAL INTENT: Standing Instructions (SI) fund transfers between customer accounts
// (one-to-one, one-to-many, loan repayments) with automated execution schedules, retry tracking,
// and failure suspension locks under Bangladesh Bank BRPD Circular No. 12 (1.8.1, 1.8.3).
public class StandingInstruction : AggregateRoot<string>
{
    public AccountNumber SourceAccountNumber { get; private set; }
    public AccountNumber TargetAccountNumber { get; private set; }
    public StandingInstructionType Type { get; private set; }
    public ExecutionFrequency Frequency { get; private set; }
    public Money TransferAmount { get; private set; }
    public Money ExecutionFee { get; private set; }
    public int? SpecificDayOfMonth { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public DateOnly NextExecutionDate { get; private set; }
    public DateOnly? LastExecutionDate { get; private set; }
    public int ConsecutiveFailureCount { get; private set; }
    public int MaxRetryFailures { get; private set; }
    public StandingInstructionStatus Status { get; private set; }
    public string? PauseReason { get; private set; }
    public DateOnly? ResumeDate { get; private set; }
    public bool IsTemporaryHoldActive { get; private set; }
    public DateOnly? HoldUntilDate { get; private set; }
    public decimal? HoldAmount { get; private set; }
    public string? HoldReason { get; private set; }
    public string Narrative { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

#pragma warning disable CS8618
    private StandingInstruction() : base(string.Empty) { }
#pragma warning restore CS8618

    public StandingInstruction(
        string instructionId,
        AccountNumber sourceAccountNumber,
        AccountNumber targetAccountNumber,
        StandingInstructionType type,
        ExecutionFrequency frequency,
        Money transferAmount,
        Money executionFee,
        DateOnly startDate,
        DateOnly? endDate = null,
        int? specificDayOfMonth = null,
        string? narrative = null,
        int maxRetryFailures = 3) : base(instructionId)
    {
        if (string.IsNullOrWhiteSpace(instructionId))
            throw new ArgumentException("Instruction ID is required.", nameof(instructionId));
        ArgumentNullException.ThrowIfNull(sourceAccountNumber, nameof(sourceAccountNumber));
        ArgumentNullException.ThrowIfNull(targetAccountNumber, nameof(targetAccountNumber));
        ArgumentNullException.ThrowIfNull(transferAmount, nameof(transferAmount));
        ArgumentNullException.ThrowIfNull(executionFee, nameof(executionFee));

        if (sourceAccountNumber == targetAccountNumber)
        {
            throw new InvariantViolationException(
                "SourceAndTargetAccountIdenticalInvariant",
                "BRPD Circular 12/2024 (1.8.1-i)",
                "Source account and target account for a Standing Instruction cannot be identical.");
        }

        if (transferAmount.Amount <= 0m)
        {
            throw new InvariantViolationException(
                "PositiveTransferAmountInvariant",
                "BRPD Circular 12/2024 (1.8.1-iv)",
                "Standing Instruction transfer amount must be strictly positive.");
        }

        if (specificDayOfMonth.HasValue && (specificDayOfMonth.Value < 1 || specificDayOfMonth.Value > 31))
        {
            throw new ArgumentOutOfRangeException(nameof(specificDayOfMonth), "Specific day of month must be between 1 and 31.");
        }

        SourceAccountNumber = sourceAccountNumber;
        TargetAccountNumber = targetAccountNumber;
        Type = type;
        Frequency = frequency;
        TransferAmount = transferAmount;
        ExecutionFee = executionFee;
        StartDate = startDate;
        EndDate = endDate;
        NextExecutionDate = startDate;
        SpecificDayOfMonth = specificDayOfMonth;
        MaxRetryFailures = maxRetryFailures > 0 ? maxRetryFailures : 3;
        ConsecutiveFailureCount = 0;
        Status = StandingInstructionStatus.Active;
        Narrative = narrative ?? $"SI-{type}-{instructionId}";
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Pause(string reason, DateOnly? resumeDate = null)
    {
        if (Status != StandingInstructionStatus.Active)
            throw new InvalidOperationException($"Cannot pause Standing Instruction in '{Status}' state.");

        Status = StandingInstructionStatus.Paused;
        PauseReason = reason;
        ResumeDate = resumeDate;
    }

    public void Resume()
    {
        if (Status != StandingInstructionStatus.Paused && Status != StandingInstructionStatus.SuspendedDueToFailures)
            throw new InvalidOperationException($"Cannot resume Standing Instruction from '{Status}' state.");

        Status = StandingInstructionStatus.Active;
        PauseReason = null;
        ResumeDate = null;
        ConsecutiveFailureCount = 0;
    }

    public void Cancel(string reason)
    {
        Status = StandingInstructionStatus.Cancelled;
        PauseReason = reason;
    }

    public void ApplyTemporaryHold(DateOnly holdUntilDate, decimal? holdAmount, string reason)
    {
        if (Status != StandingInstructionStatus.Active)
            throw new InvalidOperationException($"Cannot apply temporary hold on Standing Instruction in '{Status}' state.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Hold reason is required.", nameof(reason));

        IsTemporaryHoldActive = true;
        HoldUntilDate = holdUntilDate;
        HoldAmount = holdAmount;
        HoldReason = reason.Trim();
    }

    public void ReleaseTemporaryHold(string approverUserId)
    {
        if (string.IsNullOrWhiteSpace(approverUserId))
            throw new ArgumentException("Approver User ID is required to release temporary hold.", nameof(approverUserId));

        IsTemporaryHoldActive = false;
        HoldUntilDate = null;
        HoldAmount = null;
        HoldReason = null;
    }

    public bool IsUnderHoldAsOf(DateOnly asOfDate)
    {
        if (!IsTemporaryHoldActive) return false;
        if (HoldUntilDate.HasValue && asOfDate >= HoldUntilDate.Value)
        {
            // Auto-expired hold
            IsTemporaryHoldActive = false;
            HoldUntilDate = null;
            HoldAmount = null;
            HoldReason = null;
            return false;
        }
        return true;
    }

    public void ModifyInstruction(
        Money? newTransferAmount = null,
        ExecutionFrequency? newFrequency = null,
        Money? newExecutionFee = null,
        AccountNumber? newTargetAccount = null,
        int? newSpecificDayOfMonth = null,
        DateOnly? newEndDate = null,
        string? modifierUserId = null)
    {
        if (Status == StandingInstructionStatus.Cancelled || Status == StandingInstructionStatus.Completed)
            throw new InvalidOperationException($"Cannot modify Standing Instruction in '{Status}' state.");

        if (newTransferAmount is not null)
        {
            if (newTransferAmount.Amount <= 0m)
                throw new InvariantViolationException("PositiveTransferAmountInvariant", "BRPD Circular 12/2024 (1.8.1-iv)", "Transfer amount must be strictly positive.");
            TransferAmount = newTransferAmount;
        }

        if (newFrequency.HasValue) Frequency = newFrequency.Value;
        if (newExecutionFee is not null) ExecutionFee = newExecutionFee;

        if (newTargetAccount is not null)
        {
            if (newTargetAccount == SourceAccountNumber)
                throw new InvariantViolationException("SourceAndTargetAccountIdenticalInvariant", "BRPD Circular 12/2024 (1.8.1-i)", "Source and target account cannot be identical.");
            TargetAccountNumber = newTargetAccount;
        }

        if (newSpecificDayOfMonth.HasValue)
        {
            if (newSpecificDayOfMonth.Value < 1 || newSpecificDayOfMonth.Value > 31)
                throw new ArgumentOutOfRangeException(nameof(newSpecificDayOfMonth), "Specific day of month must be between 1 and 31.");
            SpecificDayOfMonth = newSpecificDayOfMonth.Value;
        }

        if (newEndDate.HasValue) EndDate = newEndDate;
    }

    public void RecordExecutionSuccess(DateOnly executionDate, string voucherNumber)
    {
        LastExecutionDate = executionDate;
        ConsecutiveFailureCount = 0;
        NextExecutionDate = CalculateNextExecutionDate(executionDate);

        if (EndDate.HasValue && NextExecutionDate > EndDate.Value)
        {
            Status = StandingInstructionStatus.Completed;
        }
    }

    public void RecordExecutionFailure(DateOnly executionDate, string failureReason)
    {
        ConsecutiveFailureCount++;
        if (ConsecutiveFailureCount >= MaxRetryFailures)
        {
            Status = StandingInstructionStatus.SuspendedDueToFailures;
            PauseReason = $"Suspended after {ConsecutiveFailureCount} consecutive failures: {failureReason}";
        }
    }

    public DateOnly CalculateNextExecutionDate(DateOnly currentDate)
    {
        return Frequency switch
        {
            ExecutionFrequency.Daily => currentDate.AddDays(1),
            ExecutionFrequency.Weekly => currentDate.AddDays(7),
            ExecutionFrequency.Monthly => SpecificDayOfMonth.HasValue
                ? new DateOnly(currentDate.AddMonths(1).Year, currentDate.AddMonths(1).Month, Math.Min(SpecificDayOfMonth.Value, DateTime.DaysInMonth(currentDate.AddMonths(1).Year, currentDate.AddMonths(1).Month)))
                : currentDate.AddMonths(1),
            ExecutionFrequency.Quarterly => currentDate.AddMonths(3),
            ExecutionFrequency.SpecificDayOfMonth => SpecificDayOfMonth.HasValue
                ? new DateOnly(currentDate.AddMonths(1).Year, currentDate.AddMonths(1).Month, Math.Min(SpecificDayOfMonth.Value, DateTime.DaysInMonth(currentDate.AddMonths(1).Year, currentDate.AddMonths(1).Month)))
                : currentDate.AddMonths(1),
            _ => currentDate.AddDays(1)
        };
    }
}
