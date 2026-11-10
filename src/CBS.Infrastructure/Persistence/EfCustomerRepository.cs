using CBS.Application.Common.Interfaces;
using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfCustomerRepository : ICustomerRepository
{
    private readonly CbsDbContext _dbContext;

    public EfCustomerRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Customer?> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == cif, cancellationToken);
    }

    public async Task<Customer?> GetByNationalIdAsync(NationalIdentityNumber nid, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.NationalId == nid, cancellationToken);
    }

    public async Task<Customer?> FindByDeduplicationFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fingerprint)) return null;

        var allCustomers = await _dbContext.Customers.ToListAsync(cancellationToken);
        return allCustomers.FirstOrDefault(c => c.ComputeDeduplicationFingerprint() == fingerprint);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _dbContext.Customers.AddAsync(customer, cancellationToken);
    }

    public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(customer);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.Customers.Update(customer);
        }
        return Task.CompletedTask;
    }
}
