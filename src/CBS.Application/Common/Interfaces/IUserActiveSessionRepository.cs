using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Enums;
using CBS.Domain.Security;

namespace CBS.Application.Common.Interfaces;

public interface IUserActiveSessionRepository
{
    Task<UserActiveSession?> GetActiveSessionByUserIdAsync(string userId, CancellationToken ct = default);
    Task<UserActiveSession?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<UserActiveSession>> GetActiveSessionsAsync(CancellationToken ct = default);
    Task AddAsync(UserActiveSession session, CancellationToken ct = default);
    Task UpdateAsync(UserActiveSession session, CancellationToken ct = default);
    Task TerminateActiveSessionsForUserAsync(string userId, SessionTerminationReason reason, string? notes = null, CancellationToken ct = default);
}
