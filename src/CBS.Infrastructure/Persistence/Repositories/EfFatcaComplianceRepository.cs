// ARCHITECTURAL INTENT: EF Core repository for FatcaComplianceProfile.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 8.1.15.

using CBS.Application.Common.Interfaces;
using CBS.Domain.Customers;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfFatcaComplianceRepository : IFatcaComplianceRepository
{
    private readonly CbsDbContext _dbContext;

    public EfFatcaComplianceRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FatcaComplianceProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.FatcaComplianceProfiles
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<FatcaComplianceProfile?> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.FatcaComplianceProfiles
            .FirstOrDefaultAsync(p => p.Cif == cif, cancellationToken);
    }

    public async Task<IReadOnlyList<FatcaComplianceProfile>> GetRecalcitrantAccountsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.FatcaComplianceProfiles
            .Where(p => p.Classification == FatcaClassification.RecalcitrantAccountHolder)
            .OrderByDescending(p => p.LastEvaluatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(FatcaComplianceProfile profile, CancellationToken cancellationToken = default)
    {
        await _dbContext.FatcaComplianceProfiles.AddAsync(profile, cancellationToken);
    }

    public Task UpdateAsync(FatcaComplianceProfile profile, CancellationToken cancellationToken = default)
    {
        _dbContext.FatcaComplianceProfiles.Update(profile);
        return Task.CompletedTask;
    }
}
