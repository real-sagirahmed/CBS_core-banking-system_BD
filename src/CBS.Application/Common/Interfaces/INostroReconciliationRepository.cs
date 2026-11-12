// ARCHITECTURAL INTENT: Repository contract for Nostro Mirror Reconciliation items.
// Conforms to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 6.4.2.

using CBS.Domain.Enums;
using CBS.Domain.Treasury;

namespace CBS.Application.Common.Interfaces;

public interface INostroReconciliationRepository
{
    Task<NostroMirrorReconciliationItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NostroMirrorReconciliationItem>> GetByAccountCodeAsync(string nostroAccountCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NostroMirrorReconciliationItem>> GetUnreconciledAsync(string? nostroAccountCode = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NostroMirrorReconciliationItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(NostroMirrorReconciliationItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(NostroMirrorReconciliationItem item, CancellationToken cancellationToken = default);
}
