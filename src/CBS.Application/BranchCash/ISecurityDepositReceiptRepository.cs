using CBS.Domain.BranchCash;

namespace CBS.Application.BranchCash;

public interface ISecurityDepositReceiptRepository
{
    Task<SecurityDepositReceipt?> GetBySdrNumberAsync(string sdrNumber, CancellationToken cancellationToken = default);
    Task<SecurityDepositReceipt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SecurityDepositReceipt>> GetByTenderNumberAsync(string tenderNumber, CancellationToken cancellationToken = default);
    Task AddAsync(SecurityDepositReceipt sdr, CancellationToken cancellationToken = default);
    Task UpdateAsync(SecurityDepositReceipt sdr, CancellationToken cancellationToken = default);

    Task AddStopPaymentJournalAsync(RemittanceStopPaymentJournal journal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RemittanceStopPaymentJournal>> GetJournalsByInstrumentAsync(string instrumentNumber, CancellationToken cancellationToken = default);
}
