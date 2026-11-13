using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.BackValue;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfLinkedReversalRepository : ILinkedReversalRepository
{
    private readonly CbsDbContext _dbContext;

    public EfLinkedReversalRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddReversalRecordAsync(LinkedTransactionReversalRecord record, CancellationToken ct = default)
    {
        await _dbContext.LinkedTransactionReversalRecords.AddAsync(record, ct);
    }

    public async Task<LinkedTransactionReversalRecord?> GetByOriginalVoucherIdAsync(string originalVoucherId, CancellationToken ct = default)
    {
        return await _dbContext.LinkedTransactionReversalRecords
            .FirstOrDefaultAsync(r => r.OriginalVoucherId == originalVoucherId, ct);
    }
}
