// ARCHITECTURAL INTENT: EF Core repository implementation for StatutoryReserveAssessment.
// Conforms to Bangladesh Bank MPD Circular No. 03/2020 and BRPD Circular No. 12 (13 May 2024).

using CBS.Application.Common.Interfaces;
using CBS.Domain.Treasury;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfStatutoryReserveRepository : IStatutoryReserveRepository
{
    private readonly CbsDbContext _dbContext;

    public EfStatutoryReserveRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<StatutoryReserveAssessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.StatutoryReserves
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<StatutoryReserveAssessment?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.StatutoryReserves
            .OrderByDescending(s => s.AssessmentDate)
            .ThenByDescending(s => s.EvaluatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StatutoryReserveAssessment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.StatutoryReserves
            .OrderByDescending(s => s.AssessmentDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(StatutoryReserveAssessment assessment, CancellationToken cancellationToken = default)
    {
        await _dbContext.StatutoryReserves.AddAsync(assessment, cancellationToken);
    }
}
