using CBS.Domain.Enums;

namespace CBS.Application.StandingInstructions.DTOs;

public sealed record MultiSweepTargetItemDto(
    string TargetAccountNumber,
    decimal AllocationPercentage,
    int PriorityOrder = 1,
    decimal? TargetBalanceCap = null);

public sealed record ConfigureMultiSweepRequest(
    string RuleId,
    MultiSweepType Type,
    string MasterAccountNumber,
    List<MultiSweepTargetItemDto> Targets,
    decimal ThresholdAmount,
    decimal MinimumSweepAmount,
    AllocationStrategy Strategy);

public sealed record MultiSweepRuleResponseDto(
    string RuleId,
    string Type,
    string MasterAccountNumber,
    List<MultiSweepTargetItemDto> Targets,
    decimal ThresholdAmount,
    decimal MinimumSweepAmount,
    string Strategy,
    bool IsActive);

public sealed record ExecuteMultiSweepRequest(string RuleId);

public sealed record MultiSweepLegDetailDto(
    string FromAccount,
    string ToAccount,
    decimal Amount,
    string VoucherNumber,
    string Narrative);

public sealed record MultiSweepExecutionResultDto(
    string RuleId,
    string SweepType,
    decimal TotalAmountSwept,
    int TotalLegsExecuted,
    List<MultiSweepLegDetailDto> Legs,
    string StatusMessage);
