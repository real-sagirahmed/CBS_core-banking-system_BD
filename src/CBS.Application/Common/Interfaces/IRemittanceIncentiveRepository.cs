using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Remittance;

namespace CBS.Application.Common.Interfaces;

public interface IRemittanceIncentiveRepository
{
    Task<RemittanceCashIncentiveRecord?> GetByIdAsync(string incentiveId, CancellationToken ct = default);
    Task<RemittanceCashIncentiveRecord?> GetByReferenceAsync(string remittanceReferenceNumber, CancellationToken ct = default);
    Task<IReadOnlyList<RemittanceCashIncentiveRecord>> GetPendingClaimsAsync(CancellationToken ct = default);
    Task AddAsync(RemittanceCashIncentiveRecord record, CancellationToken ct = default);
    Task UpdateAsync(RemittanceCashIncentiveRecord record, CancellationToken ct = default);
}
