// ARCHITECTURAL INTENT: EF Core Repository implementation for ContinuousLoanFacility aggregate
// enforcing BRPD Circular 12 (4.1, 3.9) query patterns for single facility lookup, DP variance,
// expired unrenewed facilities, and overdue stock statement monitoring.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Lending.Continuous;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfContinuousLoanRepository : IContinuousLoanRepository
{
    private readonly CbsDbContext _dbContext;

    public EfContinuousLoanRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ContinuousLoanFacility?> GetByAccountNumberAsync(
        AccountNumber accountNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContinuousLoanFacilities
            .FirstOrDefaultAsync(c => c.Id == accountNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<ContinuousLoanFacility>> GetByCifAsync(
        CifNumber cif,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContinuousLoanFacilities
            .Where(c => c.Cif == cif)
            .OrderByDescending(c => c.SanctionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContinuousLoanFacility>> GetDrawingPowerDeficitFacilitiesAsync(
        string? branchCode,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ContinuousLoanFacilities.AsQueryable();

        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var branch = branchCode.Trim();
            query = query.Where(c => c.BranchCode == branch);
        }

        var list = await query.ToListAsync(cancellationToken);
        return list.Where(c => c.OutstandingPrincipal.Amount > Math.Min(c.SanctionLimit.Amount, c.DrawingPower.Amount)).ToList();
    }

    public async Task<IReadOnlyList<ContinuousLoanFacility>> GetExpiredUnrenewedFacilitiesAsync(
        DateOnly asOfDate,
        string? branchCode,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ContinuousLoanFacilities.AsQueryable();

        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var branch = branchCode.Trim();
            query = query.Where(c => c.BranchCode == branch);
        }

        return await query
            .Where(c => c.ExpiryDate < asOfDate)
            .OrderBy(c => c.ExpiryDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContinuousLoanFacility>> GetOverdueStockStatementFacilitiesAsync(
        DateOnly asOfDate,
        int thresholdDays,
        string? branchCode,
        CancellationToken cancellationToken = default)
    {
        var cutoff = asOfDate.AddDays(-thresholdDays);
        var query = _dbContext.ContinuousLoanFacilities.AsQueryable();

        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var branch = branchCode.Trim();
            query = query.Where(c => c.BranchCode == branch);
        }

        return await query
            .Where(c => c.LastStockStatementDate == null || c.LastStockStatementDate <= cutoff)
            .OrderBy(c => c.LastStockStatementDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ContinuousLoanFacility facility, CancellationToken cancellationToken = default)
    {
        await _dbContext.ContinuousLoanFacilities.AddAsync(facility, cancellationToken);
    }

    public Task UpdateAsync(ContinuousLoanFacility facility, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Entry(facility).State == EntityState.Detached)
        {
            _dbContext.ContinuousLoanFacilities.Update(facility);
        }
        return Task.CompletedTask;
    }

    public async Task AddStockStatementAsync(ContinuousLoanStockStatement statement, CancellationToken cancellationToken = default)
    {
        await _dbContext.ContinuousLoanStockStatements.AddAsync(statement, cancellationToken);
    }

    public async Task AddGodownInspectionAsync(ContinuousLoanGodownInspection inspection, CancellationToken cancellationToken = default)
    {
        await _dbContext.ContinuousLoanGodownInspections.AddAsync(inspection, cancellationToken);
    }

    public async Task AddTodRecordAsync(TemporaryOverdraftRecord todRecord, CancellationToken cancellationToken = default)
    {
        await _dbContext.ContinuousLoanTodRecords.AddAsync(todRecord, cancellationToken);
    }

    public async Task AddRenewalRecordAsync(ContinuousLoanRenewalRecord renewalRecord, CancellationToken cancellationToken = default)
    {
        await _dbContext.ContinuousLoanRenewalRecords.AddAsync(renewalRecord, cancellationToken);
    }

    public async Task<IReadOnlyList<ContinuousLoanStockStatement>> GetStockStatementsByAccountAsync(
        string loanAccountNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContinuousLoanStockStatements
            .Where(s => s.LoanAccountNumber == loanAccountNumber)
            .OrderByDescending(s => s.StatementDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContinuousLoanGodownInspection>> GetGodownInspectionsByAccountAsync(
        string loanAccountNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContinuousLoanGodownInspections
            .Where(g => g.LoanAccountNumber == loanAccountNumber)
            .OrderByDescending(g => g.InspectionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TemporaryOverdraftRecord>> GetTodRecordsByAccountAsync(
        string loanAccountNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContinuousLoanTodRecords
            .Where(t => t.LoanAccountNumber == loanAccountNumber)
            .OrderByDescending(t => t.GrantDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContinuousLoanRenewalRecord>> GetRenewalRecordsByAccountAsync(
        string loanAccountNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContinuousLoanRenewalRecords
            .Where(r => r.LoanAccountNumber == loanAccountNumber)
            .OrderByDescending(r => r.RenewalDate)
            .ToListAsync(cancellationToken);
    }
}
