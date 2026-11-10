using CBS.Application.Common.Interfaces;
using CBS.Domain.PaymentRails;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfRtgsRepository : IRtgsRepository
{
    private readonly CbsDbContext _dbContext;

    public EfRtgsRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<RtgsTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RtgsTransactions
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<RtgsTransaction?> GetByEndToEndIdAsync(string endToEndId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RtgsTransactions
            .FirstOrDefaultAsync(r => r.EndToEndId == endToEndId.Trim().ToUpperInvariant(), cancellationToken);
    }

    public async Task AddAsync(RtgsTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.RtgsTransactions.AddAsync(transaction, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RtgsTransaction transaction, CancellationToken cancellationToken = default)
    {
        _dbContext.RtgsTransactions.Update(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
