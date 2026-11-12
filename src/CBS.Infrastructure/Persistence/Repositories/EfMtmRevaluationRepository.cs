// ARCHITECTURAL INTENT: EF Core repository implementation for MtmRevaluationBatch.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) and DOS Circular 05/2008.

using CBS.Application.Common.Interfaces;
using CBS.Domain.Treasury;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfMtmRevaluationRepository : IMtmRevaluationRepository
{
    private readonly CbsDbContext _dbContext;

    public EfMtmRevaluationRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<MtmRevaluationBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MtmRevaluationBatches
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MtmRevaluationBatch>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.MtmRevaluationBatches
            .OrderByDescending(m => m.ExecutedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MtmRevaluationBatch batch, CancellationToken cancellationToken = default)
    {
        await _dbContext.MtmRevaluationBatches.AddAsync(batch, cancellationToken);
    }
}
