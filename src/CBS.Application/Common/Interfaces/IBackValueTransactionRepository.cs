using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.BackValue;

namespace CBS.Application.Common.Interfaces;

public interface IBackValueTransactionRepository
{
    Task<BackValueTransactionRequest?> GetByIdAsync(string requestId, CancellationToken ct = default);
    Task<IReadOnlyList<BackValueTransactionRequest>> GetPendingRequestsAsync(CancellationToken ct = default);
    Task AddAsync(BackValueTransactionRequest request, CancellationToken ct = default);
    Task UpdateAsync(BackValueTransactionRequest request, CancellationToken ct = default);
}
