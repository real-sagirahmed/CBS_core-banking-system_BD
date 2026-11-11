using CBS.Domain.Enums;
using CBS.Domain.Security;

namespace CBS.Application.Security;

public interface ISystemAdminRepository
{
    Task<SystemUserProfile?> GetUserByIdAsync(string userId, CancellationToken ct = default);
    Task<SystemUserProfile?> GetUserByEmployeeIdAsync(string employeeId, CancellationToken ct = default);
    Task AddUserAsync(SystemUserProfile user, CancellationToken ct = default);
    Task UpdateUserAsync(SystemUserProfile user, CancellationToken ct = default);
    Task<IEnumerable<SystemUserProfile>> GetAllUsersAsync(CancellationToken ct = default);

    Task<AdminActionProposal?> GetProposalByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<AdminActionProposal>> GetPendingProposalsAsync(CancellationToken ct = default);
    Task AddProposalAsync(AdminActionProposal proposal, CancellationToken ct = default);
    Task UpdateProposalAsync(AdminActionProposal proposal, CancellationToken ct = default);

    Task<TransactionDoaOverride?> GetOverrideByIdAsync(Guid id, CancellationToken ct = default);
    Task AddOverrideAsync(TransactionDoaOverride overrideItem, CancellationToken ct = default);
    Task UpdateOverrideAsync(TransactionDoaOverride overrideItem, CancellationToken ct = default);

    Task AddAuditJournalAsync(AdminAuditJournal journal, CancellationToken ct = default);
    Task<IEnumerable<AdminAuditJournal>> GetAuditJournalsAsync(JournalCategory? category, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
}
