using CBS.Application.Common.Interfaces;
using CBS.Domain.BranchCash;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfTellerDrawerRepository : ITellerDrawerRepository
{
    private readonly CbsDbContext _dbContext;

    public EfTellerDrawerRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<TellerDrawer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TellerDrawers
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<TellerDrawer?> GetActiveDrawerByCashierAsync(string cashierUserId, DateOnly operatingDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TellerDrawers
            .FirstOrDefaultAsync(d => d.CashierUserId == cashierUserId && d.OperatingDate == operatingDate, cancellationToken);
    }

    public async Task AddAsync(TellerDrawer drawer, CancellationToken cancellationToken = default)
    {
        await _dbContext.TellerDrawers.AddAsync(drawer, cancellationToken);
    }

    public Task UpdateAsync(TellerDrawer drawer, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(drawer);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.TellerDrawers.Update(drawer);
        }
        return Task.CompletedTask;
    }
}
