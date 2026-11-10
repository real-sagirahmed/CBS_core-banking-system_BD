using CBS.Domain.Enums;

namespace CBS.Application.StandingInstructions.DTOs;

public sealed record CreateStandingInstructionRequest(
    string InstructionId,
    string SourceAccountNumber,
    string TargetAccountNumber,
    StandingInstructionType Type,
    ExecutionFrequency Frequency,
    decimal TransferAmount,
    decimal ExecutionFee,
    DateOnly StartDate,
    DateOnly? EndDate = null,
    int? SpecificDayOfMonth = null,
    string? Narrative = null);

public sealed record ConfigureSweepRuleRequest(
    string RuleId,
    string PrimaryAccountNumber,
    string SecondaryAccountNumber,
    decimal UpperThreshold,
    decimal LowerThreshold,
    decimal MinimumSweepAmount);

public sealed record StandingInstructionResponseDto(
    string InstructionId,
    string SourceAccountNumber,
    string TargetAccountNumber,
    string Type,
    string Frequency,
    decimal TransferAmount,
    decimal ExecutionFee,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly NextExecutionDate,
    string Status,
    int ConsecutiveFailureCount,
    string? PauseReason,
    string Narrative);

public sealed record SweepRuleResponseDto(
    string RuleId,
    string PrimaryAccountNumber,
    string SecondaryAccountNumber,
    decimal UpperThreshold,
    decimal LowerThreshold,
    decimal MinimumSweepAmount,
    bool IsActive);

public sealed record ExecuteSweepResponseDto(
    string RuleId,
    string? SweepDirection,
    decimal AmountSwept,
    decimal NewPrimaryBalance,
    decimal NewSecondaryBalance,
    string? VoucherNumber,
    string StatusMessage);
