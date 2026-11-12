// ARCHITECTURAL INTENT: EF Core repository for BorrowerIcrrAssessment.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (Module 8.1.16) and BRPD Circular No. 03/2019.

using CBS.Application.Common.Interfaces;
using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfBorrowerIcrrRepository : IBorrowerIcrrRepository
{
    private readonly CbsDbContext _dbContext;

    public EfBorrowerIcrrRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BorrowerIcrrAssessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BorrowerIcrrAssessments
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<BorrowerIcrrAssessment?> GetLatestByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BorrowerIcrrAssessments
            .Where(a => a.Cif == cif)
            .OrderByDescending(a => a.AssessmentDate)
            .ThenByDescending(a => a.EvaluatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BorrowerIcrrAssessment>> GetAllByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BorrowerIcrrAssessments
            .Where(a => a.Cif == cif)
            .OrderByDescending(a => a.AssessmentDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BorrowerIcrrAssessment assessment, CancellationToken cancellationToken = default)
    {
        await _dbContext.BorrowerIcrrAssessments.AddAsync(assessment, cancellationToken);
    }

    public Task UpdateAsync(BorrowerIcrrAssessment assessment, CancellationToken cancellationToken = default)
    {
        _dbContext.BorrowerIcrrAssessments.Update(assessment);
        return Task.CompletedTask;
    }
}
