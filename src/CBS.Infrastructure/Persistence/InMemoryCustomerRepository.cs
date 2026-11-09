using System.Collections.Concurrent;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;

namespace CBS.Infrastructure.Persistence;

public class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly ConcurrentDictionary<string, Customer> _customersByCif = new();

    public Task<Customer?> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cif);
        _customersByCif.TryGetValue(cif.Value, out var customer);
        return Task.FromResult(customer);
    }

    public Task<Customer?> GetByNationalIdAsync(NationalIdentityNumber nid, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nid);
        var customer = _customersByCif.Values.FirstOrDefault(c => c.NationalId?.Value == nid.Value);
        return Task.FromResult(customer);
    }

    public Task<Customer?> FindByDeduplicationFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fingerprint)) return Task.FromResult<Customer?>(null);
        var customer = _customersByCif.Values.FirstOrDefault(c => c.ComputeDeduplicationFingerprint() == fingerprint);
        return Task.FromResult(customer);
    }

    public Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);
        if (!_customersByCif.TryAdd(customer.Id.Value, customer))
        {
            throw new InvalidOperationException($"Customer with CIF '{customer.Id.Value}' already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);
        _customersByCif[customer.Id.Value] = customer;
        return Task.CompletedTask;
    }
}
