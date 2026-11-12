using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Security;

namespace CBS.Application.Common.Interfaces;

public interface IAdminChangeRequestRepository
{
    Task<AdminChangeRequest?> GetByIdAsync(string requestId, CancellationToken ct = default);
    Task<IReadOnlyList<AdminChangeRequest>> GetPendingRequestsAsync(CancellationToken ct = default);
    Task AddAsync(AdminChangeRequest request, CancellationToken ct = default);
    Task UpdateAsync(AdminChangeRequest request, CancellationToken ct = default);
}
