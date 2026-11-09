namespace CBS.Application.GeneralBanking.Commands.RunBranchEodBatch;

public record RunBranchEodBatchCommand(
    string BranchCode,
    string UserId);
