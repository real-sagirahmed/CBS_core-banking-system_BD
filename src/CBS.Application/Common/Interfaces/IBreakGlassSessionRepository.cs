using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Security;

namespace CBS.Application.Common.Interfaces;

public interface IBreakGlassSessionRepository
{
    Task<BreakGlassEmergencySession?> GetByIdAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<BreakGlassEmergencySession>> GetActiveSessionsAsync(CancellationToken ct = default);
    Task AddAsync(BreakGlassEmergencySession session, CancellationToken ct = default);
    Task UpdateAsync(BreakGlassEmergencySession session, CancellationToken ct = default);
}
