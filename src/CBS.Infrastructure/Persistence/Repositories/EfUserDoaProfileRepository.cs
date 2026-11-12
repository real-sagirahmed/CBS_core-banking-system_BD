using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfUserDoaProfileRepository : IUserDoaProfileRepository
{
    private readonly CbsDbContext _dbContext;

    public EfUserDoaProfileRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserDoaProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var normalized = userId.Trim().ToUpperInvariant();
        return await _dbContext.UserDoaProfiles
            .FirstOrDefaultAsync(p => p.UserId == normalized && p.IsActive, ct);
    }

    public async Task AddAsync(UserDoaProfile profile, CancellationToken ct = default)
    {
        await _dbContext.UserDoaProfiles.AddAsync(profile, ct);
    }

    public Task UpdateAsync(UserDoaProfile profile, CancellationToken ct = default)
    {
        _dbContext.UserDoaProfiles.Update(profile);
        return Task.CompletedTask;
    }
}
