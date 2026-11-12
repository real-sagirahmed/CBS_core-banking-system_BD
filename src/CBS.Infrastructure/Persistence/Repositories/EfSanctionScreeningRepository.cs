// ARCHITECTURAL INTENT: EF Core repository for SanctionScreeningRecord.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 8.1.14.

using CBS.Application.Common.Interfaces;
using CBS.Domain.Customers;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfSanctionScreeningRepository : ISanctionScreeningRepository
{
    private readonly CbsDbContext _dbContext;

    public EfSanctionScreeningRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SanctionScreeningRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SanctionScreeningRecords
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SanctionScreeningRecord>> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SanctionScreeningRecords
            .Where(r => r.Cif == cif)
            .OrderByDescending(r => r.ScreenedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SanctionScreeningRecord>> GetPendingInvestigationsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SanctionScreeningRecords
            .Where(r => r.Status == SanctionMatchStatus.PotentialMatch)
            .OrderByDescending(r => r.ScreenedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SanctionScreeningRecord record, CancellationToken cancellationToken = default)
    {
        await _dbContext.SanctionScreeningRecords.AddAsync(record, cancellationToken);
    }

    public Task UpdateAsync(SanctionScreeningRecord record, CancellationToken cancellationToken = default)
    {
        _dbContext.SanctionScreeningRecords.Update(record);
        return Task.CompletedTask;
    }
}
