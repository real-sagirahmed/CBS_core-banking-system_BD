using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Security;

namespace CBS.Application.Common.Interfaces;

public interface ITransactionAnomalyRepository
{
    Task<TransactionAnomalyRecord?> GetByIdAsync(string anomalyId, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionAnomalyRecord>> GetPendingAnomaliesAsync(CancellationToken ct = default);
    Task AddAsync(TransactionAnomalyRecord record, CancellationToken ct = default);
    Task UpdateAsync(TransactionAnomalyRecord record, CancellationToken ct = default);
}
