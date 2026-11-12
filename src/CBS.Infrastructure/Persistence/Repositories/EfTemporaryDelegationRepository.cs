using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfTemporaryDelegationRepository : ITemporaryDelegationRepository
{
    private readonly CbsDbContext _dbContext;

    public EfTemporaryDelegationRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TemporaryAuthorityDelegation?> GetByIdAsync(string delegationId, CancellationToken ct = default)
    {
        return await _dbContext.TemporaryAuthorityDelegations
            .FirstOrDefaultAsync(d => d.Id == delegationId.Trim().ToUpperInvariant(), ct);
    }

    public async Task<TemporaryAuthorityDelegation?> GetActiveDelegationAsync(string delegateeUserId, DateTime currentUtc, CancellationToken ct = default)
    {
        var normalized = delegateeUserId.Trim().ToUpperInvariant();
        return await _dbContext.TemporaryAuthorityDelegations
            .FirstOrDefaultAsync(d =>
                d.DelegateeUserId == normalized &&
                d.Status == DelegationStatus.Active &&
                d.EffectiveFromUtc <= currentUtc &&
                d.EffectiveToUtc >= currentUtc, ct);
    }

    public async Task<IReadOnlyList<TemporaryAuthorityDelegation>> GetActiveDelegationsAsync(CancellationToken ct = default)
    {
        return await _dbContext.TemporaryAuthorityDelegations
            .Where(d => d.Status == DelegationStatus.Active)
            .OrderByDescending(d => d.EffectiveFromUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(TemporaryAuthorityDelegation delegation, CancellationToken ct = default)
    {
        await _dbContext.TemporaryAuthorityDelegations.AddAsync(delegation, ct);
    }

    public Task UpdateAsync(TemporaryAuthorityDelegation delegation, CancellationToken ct = default)
    {
        _dbContext.TemporaryAuthorityDelegations.Update(delegation);
        return Task.CompletedTask;
    }
}
