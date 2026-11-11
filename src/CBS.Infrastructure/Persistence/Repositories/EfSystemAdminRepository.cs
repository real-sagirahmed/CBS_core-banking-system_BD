using CBS.Application.Security;
using CBS.Domain.Enums;
using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfSystemAdminRepository : ISystemAdminRepository
{
    private readonly CbsDbContext _dbContext;

    public EfSystemAdminRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<SystemUserProfile?> GetUserByIdAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        string upper = userId.Trim().ToUpperInvariant();
        return await _dbContext.SystemUserProfiles
            .FirstOrDefaultAsync(u => u.Id == upper, ct);
    }

    public async Task<SystemUserProfile?> GetUserByEmployeeIdAsync(string employeeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(employeeId)) return null;
        string trim = employeeId.Trim();
        return await _dbContext.SystemUserProfiles
            .FirstOrDefaultAsync(u => u.EmployeeId == trim, ct);
    }

    public async Task AddUserAsync(SystemUserProfile user, CancellationToken ct = default)
    {
        await _dbContext.SystemUserProfiles.AddAsync(user, ct);
    }

    public Task UpdateUserAsync(SystemUserProfile user, CancellationToken ct = default)
    {
        _dbContext.SystemUserProfiles.Update(user);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<SystemUserProfile>> GetAllUsersAsync(CancellationToken ct = default)
    {
        return await _dbContext.SystemUserProfiles
            .OrderBy(u => u.Id)
            .ToListAsync(ct);
    }

    public async Task<AdminActionProposal?> GetProposalByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.AdminActionProposals
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IEnumerable<AdminActionProposal>> GetPendingProposalsAsync(CancellationToken ct = default)
    {
        return await _dbContext.AdminActionProposals
            .Where(p => p.Status == AdminProposalStatus.PendingApproval)
            .OrderByDescending(p => p.ProposedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddProposalAsync(AdminActionProposal proposal, CancellationToken ct = default)
    {
        await _dbContext.AdminActionProposals.AddAsync(proposal, ct);
    }

    public Task UpdateProposalAsync(AdminActionProposal proposal, CancellationToken ct = default)
    {
        _dbContext.AdminActionProposals.Update(proposal);
        return Task.CompletedTask;
    }

    public async Task<TransactionDoaOverride?> GetOverrideByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.TransactionDoaOverrides
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task AddOverrideAsync(TransactionDoaOverride overrideItem, CancellationToken ct = default)
    {
        await _dbContext.TransactionDoaOverrides.AddAsync(overrideItem, ct);
    }

    public Task UpdateOverrideAsync(TransactionDoaOverride overrideItem, CancellationToken ct = default)
    {
        _dbContext.TransactionDoaOverrides.Update(overrideItem);
        return Task.CompletedTask;
    }

    public async Task AddAuditJournalAsync(AdminAuditJournal journal, CancellationToken ct = default)
    {
        await _dbContext.AdminAuditJournals.AddAsync(journal, ct);
    }

    public async Task<IEnumerable<AdminAuditJournal>> GetAuditJournalsAsync(
        JournalCategory? category,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var query = _dbContext.AdminAuditJournals.AsQueryable();

        if (category.HasValue)
        {
            query = query.Where(j => j.Category == category.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(j => j.TimestampUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(j => j.TimestampUtc <= toUtc.Value);
        }

        return await query
            .OrderByDescending(j => j.TimestampUtc)
            .Take(500)
            .ToListAsync(ct);
    }
}
