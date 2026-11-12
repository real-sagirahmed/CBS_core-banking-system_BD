using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfUserPasswordCredentialRepository : IUserPasswordCredentialRepository
{
    private readonly CbsDbContext _dbContext;

    public EfUserPasswordCredentialRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserPasswordCredential?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var normalized = userId.Trim().ToUpperInvariant();
        return await _dbContext.UserPasswordCredentials
            .FirstOrDefaultAsync(c => c.Id == normalized, ct);
    }

    public async Task AddAsync(UserPasswordCredential credential, CancellationToken ct = default)
    {
        await _dbContext.UserPasswordCredentials.AddAsync(credential, ct);
    }

    public Task UpdateAsync(UserPasswordCredential credential, CancellationToken ct = default)
    {
        _dbContext.UserPasswordCredentials.Update(credential);
        return Task.CompletedTask;
    }
}
