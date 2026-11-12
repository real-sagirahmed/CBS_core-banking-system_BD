// ARCHITECTURAL INTENT: EF Core repository for CustomerRiskAssessment.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 8.1.13.

using CBS.Application.Common.Interfaces;
using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfCustomerRiskAssessmentRepository : ICustomerRiskAssessmentRepository
{
    private readonly CbsDbContext _dbContext;

    public EfCustomerRiskAssessmentRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerRiskAssessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CustomerRiskAssessments
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<CustomerRiskAssessment?> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CustomerRiskAssessments
            .FirstOrDefaultAsync(a => a.Cif == cif, cancellationToken);
    }

    public async Task AddAsync(CustomerRiskAssessment assessment, CancellationToken cancellationToken = default)
    {
        await _dbContext.CustomerRiskAssessments.AddAsync(assessment, cancellationToken);
    }

    public Task UpdateAsync(CustomerRiskAssessment assessment, CancellationToken cancellationToken = default)
    {
        _dbContext.CustomerRiskAssessments.Update(assessment);
        return Task.CompletedTask;
    }
}
