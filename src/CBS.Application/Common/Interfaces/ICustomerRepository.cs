using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task<Customer?> GetByNationalIdAsync(NationalIdentityNumber nid, CancellationToken cancellationToken = default);
    Task<Customer?> FindByDeduplicationFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default);
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
    Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
}
