using CBS.Application.Common.Interfaces;
using CBS.Domain.BranchCash;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfRemittanceInstrumentRepository : IRemittanceInstrumentRepository
{
    private readonly CbsDbContext _dbContext;

    public EfRemittanceInstrumentRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<RemittanceInstrument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RemittanceInstruments
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<RemittanceInstrument?> GetByInstrumentNumberAsync(string instrumentNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RemittanceInstruments
            .FirstOrDefaultAsync(r => r.InstrumentNumber == instrumentNumber.Trim().ToUpperInvariant(), cancellationToken);
    }

    public async Task AddAsync(RemittanceInstrument instrument, CancellationToken cancellationToken = default)
    {
        await _dbContext.RemittanceInstruments.AddAsync(instrument, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RemittanceInstrument instrument, CancellationToken cancellationToken = default)
    {
        _dbContext.RemittanceInstruments.Update(instrument);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
