using CBS.Domain.Enums;

namespace CBS.Application.StandingInstructions.DTOs;

public sealed record ApplyTemporaryHoldRequest(
    DateOnly HoldUntilDate,
    decimal? HoldAmount,
    string Reason);

public sealed record ReleaseTemporaryHoldRequest(
    string ApproverUserId);

public sealed record ModifyStandingInstructionRequest(
    decimal? NewTransferAmount = null,
    ExecutionFrequency? NewFrequency = null,
    decimal? NewExecutionFee = null,
    string? NewTargetAccountNumber = null,
    int? NewSpecificDayOfMonth = null,
    DateOnly? NewEndDate = null,
    string? ModifierUserId = null);
