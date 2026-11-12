using CBS.Application.Common.Interfaces;
using CBS.Domain.TradeFinance;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfVesselTrackingRepository : IVesselTrackingRepository
{
    private readonly CbsDbContext _dbContext;

    public EfVesselTrackingRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(VesselTrackingRecord vessel, CancellationToken cancellationToken = default)
    {
        await _dbContext.VesselTrackingRecords.AddAsync(vessel, cancellationToken);
    }

    public async Task<IReadOnlyList<VesselTrackingRecord>> GetByImoOrNameAsync(string query, CancellationToken cancellationToken = default)
    {
        string q = query.Trim().ToUpperInvariant();
        return await _dbContext.VesselTrackingRecords
            .Where(v => v.ImoNumber.Contains(q) || v.VesselName.Contains(q))
            .OrderByDescending(v => v.TrackedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
