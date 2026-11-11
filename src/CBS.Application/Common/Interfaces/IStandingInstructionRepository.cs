using CBS.Domain.StandingInstructions;

namespace CBS.Application.Common.Interfaces;

public interface IStandingInstructionRepository
{
    Task<StandingInstruction?> GetByIdAsync(string instructionId, CancellationToken ct = default);
    Task<IEnumerable<StandingInstruction>> GetBySourceAccountAsync(string sourceAccountNumber, CancellationToken ct = default);
    Task<IEnumerable<StandingInstruction>> GetDueInstructionsAsync(DateOnly date, CancellationToken ct = default);
    Task AddAsync(StandingInstruction instruction, CancellationToken ct = default);
    Task UpdateAsync(StandingInstruction instruction, CancellationToken ct = default);

    // Sweep Rule operations
    Task<SweepAccountRule?> GetSweepRuleByIdAsync(string ruleId, CancellationToken ct = default);
    Task<IEnumerable<SweepAccountRule>> GetActiveSweepRulesByAccountAsync(string accountNumber, CancellationToken ct = default);
    Task AddSweepRuleAsync(SweepAccountRule rule, CancellationToken ct = default);
    Task UpdateSweepRuleAsync(SweepAccountRule rule, CancellationToken ct = default);

    // Multi-Account Sweep Rule operations (BRPD 1.8.1-i, ii)
    Task<MultiAccountSweepRule?> GetMultiSweepRuleByIdAsync(string ruleId, CancellationToken ct = default);
    Task<IEnumerable<MultiAccountSweepRule>> GetMultiSweepRulesByAccountAsync(string accountNumber, CancellationToken ct = default);
    Task AddMultiSweepRuleAsync(MultiAccountSweepRule rule, CancellationToken ct = default);
    Task UpdateMultiSweepRuleAsync(MultiAccountSweepRule rule, CancellationToken ct = default);
}
