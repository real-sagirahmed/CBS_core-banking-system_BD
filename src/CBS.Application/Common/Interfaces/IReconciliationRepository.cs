using CBS.Domain.Reconciliation;

namespace CBS.Application.Common.Interfaces;

public interface IReconciliationRepository
{
    Task<SettlementReconciliationBatch?> GetBatchByIdAsync(string batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SettlementReconciliationBatch>> GetAllBatchesAsync(CancellationToken cancellationToken = default);
    Task AddBatchAsync(SettlementReconciliationBatch batch, CancellationToken cancellationToken = default);

    Task<DisputeClaim?> GetDisputeByIdAsync(string claimId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DisputeClaim>> GetAllDisputesAsync(CancellationToken cancellationToken = default);
    Task AddDisputeAsync(DisputeClaim claim, CancellationToken cancellationToken = default);
}
