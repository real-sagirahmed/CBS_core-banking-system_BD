using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfCustomerTransactionProfileRepository : ICustomerTransactionProfileRepository
{
    private readonly CbsDbContext _dbContext;

    public EfCustomerTransactionProfileRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerTransactionProfile?> GetByCifAsync(string cifNumber, CancellationToken ct = default)
    {
        return await _dbContext.CustomerTransactionProfiles
            .FirstOrDefaultAsync(tp => tp.Id == cifNumber.Trim().ToUpperInvariant(), ct);
    }

    public async Task AddAsync(CustomerTransactionProfile profile, CancellationToken ct = default)
    {
        await _dbContext.CustomerTransactionProfiles.AddAsync(profile, ct);
    }

    public Task UpdateAsync(CustomerTransactionProfile profile, CancellationToken ct = default)
    {
        _dbContext.CustomerTransactionProfiles.Update(profile);
        return Task.CompletedTask;
    }
}
