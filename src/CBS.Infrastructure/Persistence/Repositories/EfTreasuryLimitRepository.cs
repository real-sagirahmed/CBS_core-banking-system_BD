// ARCHITECTURAL INTENT: EF Core repository implementation for TreasuryLimitProfile.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 6.2 & 6.3.

using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.Treasury;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfTreasuryLimitRepository : ITreasuryLimitRepository
{
    private readonly CbsDbContext _dbContext;

    public EfTreasuryLimitRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<TreasuryLimitProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TreasuryLimits
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<TreasuryLimitProfile?> GetActiveByTypeAndIdentifierAsync(TreasuryLimitType limitType, string identifier, CancellationToken cancellationToken = default)
    {
        string id = identifier.Trim().ToUpperInvariant();
        return await _dbContext.TreasuryLimits
            .FirstOrDefaultAsync(t => t.LimitType == limitType && t.Identifier == id && t.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<TreasuryLimitProfile>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.TreasuryLimits
            .Where(t => t.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TreasuryLimitProfile profile, CancellationToken cancellationToken = default)
    {
        await _dbContext.TreasuryLimits.AddAsync(profile, cancellationToken);
    }

    public Task UpdateAsync(TreasuryLimitProfile profile, CancellationToken cancellationToken = default)
    {
        _dbContext.TreasuryLimits.Update(profile);
        return Task.CompletedTask;
    }
}
