using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.Remittance;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfRemittanceIncentiveRepository : IRemittanceIncentiveRepository
{
    private readonly CbsDbContext _dbContext;

    public EfRemittanceIncentiveRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RemittanceCashIncentiveRecord?> GetByIdAsync(string incentiveId, CancellationToken ct = default)
    {
        return await _dbContext.RemittanceCashIncentives
            .FirstOrDefaultAsync(r => r.Id == incentiveId.Trim().ToUpperInvariant(), ct);
    }

    public async Task<RemittanceCashIncentiveRecord?> GetByReferenceAsync(string remittanceReferenceNumber, CancellationToken ct = default)
    {
        var normalized = remittanceReferenceNumber.Trim();
        return await _dbContext.RemittanceCashIncentives
            .FirstOrDefaultAsync(r => r.RemittanceReferenceNumber == normalized, ct);
    }

    public async Task<IReadOnlyList<RemittanceCashIncentiveRecord>> GetPendingClaimsAsync(CancellationToken ct = default)
    {
        return await _dbContext.RemittanceCashIncentives
            .Where(r => r.ClaimStatus == RemittanceIncentiveClaimStatus.PendingClaimSubmission)
            .OrderByDescending(r => r.DisbursedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(RemittanceCashIncentiveRecord record, CancellationToken ct = default)
    {
        await _dbContext.RemittanceCashIncentives.AddAsync(record, ct);
    }

    public Task UpdateAsync(RemittanceCashIncentiveRecord record, CancellationToken ct = default)
    {
        _dbContext.RemittanceCashIncentives.Update(record);
        return Task.CompletedTask;
    }
}
