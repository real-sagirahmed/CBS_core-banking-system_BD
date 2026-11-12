using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Security;

namespace CBS.Application.Common.Interfaces;

public interface ITemporaryDelegationRepository
{
    Task<TemporaryAuthorityDelegation?> GetByIdAsync(string delegationId, CancellationToken ct = default);
    Task<TemporaryAuthorityDelegation?> GetActiveDelegationAsync(string delegateeUserId, DateTime currentUtc, CancellationToken ct = default);
    Task<IReadOnlyList<TemporaryAuthorityDelegation>> GetActiveDelegationsAsync(CancellationToken ct = default);
    Task AddAsync(TemporaryAuthorityDelegation delegation, CancellationToken ct = default);
    Task UpdateAsync(TemporaryAuthorityDelegation delegation, CancellationToken ct = default);
}
