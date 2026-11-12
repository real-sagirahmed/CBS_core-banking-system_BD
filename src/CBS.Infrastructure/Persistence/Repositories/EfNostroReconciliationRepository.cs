// ARCHITECTURAL INTENT: EF Core repository implementation for NostroMirrorReconciliationItem.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 6.4.2.

using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.Treasury;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfNostroReconciliationRepository : INostroReconciliationRepository
{
    private readonly CbsDbContext _dbContext;

    public EfNostroReconciliationRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<NostroMirrorReconciliationItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.NostroReconciliations
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<NostroMirrorReconciliationItem>> GetByAccountCodeAsync(string nostroAccountCode, CancellationToken cancellationToken = default)
    {
        string code = nostroAccountCode.Trim().ToUpperInvariant();
        return await _dbContext.NostroReconciliations
            .Where(n => n.NostroAccountCode == code)
            .OrderBy(n => n.ValueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NostroMirrorReconciliationItem>> GetUnreconciledAsync(string? nostroAccountCode = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.NostroReconciliations
            .Where(n => n.Status == NostroReconciliationStatus.Unmatched || n.Status == NostroReconciliationStatus.Provisioned);

        if (!string.IsNullOrWhiteSpace(nostroAccountCode))
        {
            string code = nostroAccountCode.Trim().ToUpperInvariant();
            query = query.Where(n => n.NostroAccountCode == code);
        }

        return await query.OrderBy(n => n.ValueDate).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NostroMirrorReconciliationItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.NostroReconciliations
            .OrderBy(n => n.ValueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(NostroMirrorReconciliationItem item, CancellationToken cancellationToken = default)
    {
        await _dbContext.NostroReconciliations.AddAsync(item, cancellationToken);
    }

    public Task UpdateAsync(NostroMirrorReconciliationItem item, CancellationToken cancellationToken = default)
    {
        _dbContext.NostroReconciliations.Update(item);
        return Task.CompletedTask;
    }
}
