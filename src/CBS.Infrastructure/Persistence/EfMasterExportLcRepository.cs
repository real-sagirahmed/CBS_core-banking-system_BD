using CBS.Application.Common.Interfaces;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfMasterExportLcRepository : IMasterExportLcRepository
{
    private readonly CbsDbContext _dbContext;

    public EfMasterExportLcRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MasterExportLc?> GetByNumberAsync(string masterLcNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MasterExportLcs
            .FirstOrDefaultAsync(m => m.Id == masterLcNumber.Trim().ToUpperInvariant(), cancellationToken);
    }

    public async Task<IReadOnlyList<MasterExportLc>> GetByExporterCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MasterExportLcs
            .Where(m => m.ExporterCif == cif)
            .OrderByDescending(m => m.IssueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MasterExportLc masterExportLc, CancellationToken cancellationToken = default)
    {
        await _dbContext.MasterExportLcs.AddAsync(masterExportLc, cancellationToken);
    }

    public Task UpdateAsync(MasterExportLc masterExportLc, CancellationToken cancellationToken = default)
    {
        _dbContext.MasterExportLcs.Update(masterExportLc);
        return Task.CompletedTask;
    }
}
