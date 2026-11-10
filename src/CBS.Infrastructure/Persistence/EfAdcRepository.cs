using CBS.Application.Common.Interfaces;
using CBS.Domain.Adc;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfAdcRepository : IAdcRepository
{
    private readonly CbsDbContext _dbContext;

    public EfAdcRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AdcTransaction?> GetByIdAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AdcTransactions
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
    }

    public async Task<AdcTransaction?> GetByStanAndRrnAsync(string stan, string rrn, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AdcTransactions
            .FirstOrDefaultAsync(t => t.Stan == stan && t.Rrn == rrn, cancellationToken);
    }

    public async Task<IReadOnlyList<AdcTransaction>> GetAllAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AdcTransactions
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AdcTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.AdcTransactions.AddAsync(transaction, cancellationToken);
    }

    public async Task<AtmTerminal?> GetTerminalByIdAsync(string terminalId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AtmTerminals
            .FirstOrDefaultAsync(t => t.Id == terminalId.ToUpper(), cancellationToken);
    }

    public async Task<IReadOnlyList<AtmTerminal>> GetAllTerminalsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.AtmTerminals
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddTerminalAsync(AtmTerminal terminal, CancellationToken cancellationToken = default)
    {
        await _dbContext.AtmTerminals.AddAsync(terminal, cancellationToken);
    }
}
