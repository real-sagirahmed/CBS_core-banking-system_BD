using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.StandingInstructions;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfStandingInstructionRepository : IStandingInstructionRepository
{
    private readonly CbsDbContext _dbContext;

    public EfStandingInstructionRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<StandingInstruction?> GetByIdAsync(string instructionId, CancellationToken ct = default)
    {
        return await _dbContext.StandingInstructions
            .FirstOrDefaultAsync(s => s.Id == instructionId, ct);
    }

    public async Task<IEnumerable<StandingInstruction>> GetBySourceAccountAsync(string sourceAccountNumber, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(sourceAccountNumber);
        return await _dbContext.StandingInstructions
            .Where(s => s.SourceAccountNumber == acc)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<StandingInstruction>> GetDueInstructionsAsync(DateOnly date, CancellationToken ct = default)
    {
        return await _dbContext.StandingInstructions
            .Where(s => s.Status == StandingInstructionStatus.Active && s.NextExecutionDate <= date)
            .ToListAsync(ct);
    }

    public async Task AddAsync(StandingInstruction instruction, CancellationToken ct = default)
    {
        await _dbContext.StandingInstructions.AddAsync(instruction, ct);
    }

    public Task UpdateAsync(StandingInstruction instruction, CancellationToken ct = default)
    {
        _dbContext.StandingInstructions.Update(instruction);
        return Task.CompletedTask;
    }

    public async Task<SweepAccountRule?> GetSweepRuleByIdAsync(string ruleId, CancellationToken ct = default)
    {
        return await _dbContext.SweepAccountRules
            .FirstOrDefaultAsync(r => r.Id == ruleId, ct);
    }

    public async Task<IEnumerable<SweepAccountRule>> GetActiveSweepRulesByAccountAsync(string accountNumber, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(accountNumber);
        return await _dbContext.SweepAccountRules
            .Where(r => r.IsActive && (r.PrimaryAccountNumber == acc || r.SecondaryAccountNumber == acc))
            .ToListAsync(ct);
    }

    public async Task AddSweepRuleAsync(SweepAccountRule rule, CancellationToken ct = default)
    {
        await _dbContext.SweepAccountRules.AddAsync(rule, ct);
    }

    public Task UpdateSweepRuleAsync(SweepAccountRule rule, CancellationToken ct = default)
    {
        _dbContext.SweepAccountRules.Update(rule);
        return Task.CompletedTask;
    }
}
