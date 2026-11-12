using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Customers;

namespace CBS.Application.Common.Interfaces;

public interface ICustomerTransactionProfileRepository
{
    Task<CustomerTransactionProfile?> GetByCifAsync(string cifNumber, CancellationToken ct = default);
    Task AddAsync(CustomerTransactionProfile profile, CancellationToken ct = default);
    Task UpdateAsync(CustomerTransactionProfile profile, CancellationToken ct = default);
}
