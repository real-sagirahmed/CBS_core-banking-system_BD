using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.BackValue;

namespace CBS.Application.Common.Interfaces;

public interface ILinkedReversalRepository
{
    Task AddReversalRecordAsync(LinkedTransactionReversalRecord record, CancellationToken ct = default);
    Task<LinkedTransactionReversalRecord?> GetByOriginalVoucherIdAsync(string originalVoucherId, CancellationToken ct = default);
}
