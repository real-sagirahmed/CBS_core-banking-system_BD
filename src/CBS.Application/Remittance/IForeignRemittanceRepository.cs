using CBS.Domain.Remittance;

namespace CBS.Application.Remittance;

public interface IForeignRemittanceRepository
{
    Task<ForeignRemittanceTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ForeignRemittanceTransaction?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForeignRemittanceTransaction>> GetPendingApprovalAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForeignRemittanceTransaction>> GetUnclaimedIncentivesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForeignRemittanceTransaction>> GetInwardRegisterAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);
    Task AddAsync(ForeignRemittanceTransaction transaction, CancellationToken cancellationToken = default);
    Task UpdateAsync(ForeignRemittanceTransaction transaction, CancellationToken cancellationToken = default);

    Task<GovernmentIncentiveClaimBatch?> GetClaimBatchByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GovernmentIncentiveClaimBatch?> GetClaimBatchByNumberAsync(string batchNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GovernmentIncentiveClaimBatch>> GetAllClaimBatchesAsync(CancellationToken cancellationToken = default);
    Task AddClaimBatchAsync(GovernmentIncentiveClaimBatch batch, CancellationToken cancellationToken = default);
    Task UpdateClaimBatchAsync(GovernmentIncentiveClaimBatch batch, CancellationToken cancellationToken = default);
}
