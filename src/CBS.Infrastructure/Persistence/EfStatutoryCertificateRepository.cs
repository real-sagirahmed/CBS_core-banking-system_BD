using CBS.Application.Common.Interfaces;
using CBS.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfStatutoryCertificateRepository : IStatutoryCertificateRepository
{
    private readonly CbsDbContext _dbContext;

    public EfStatutoryCertificateRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(StatutoryCertificateRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record, nameof(record));
        await _dbContext.StatutoryCertificateRecords.AddAsync(record, cancellationToken);
    }

    public async Task<StatutoryCertificateRecord?> GetByCertificateNumberAsync(
        string certificateNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.StatutoryCertificateRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CertificateNumber == certificateNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<StatutoryCertificateRecord>> GetByAccountAsync(
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.StatutoryCertificateRecords
            .AsNoTracking()
            .Where(c => c.AccountNumber == accountNumber)
            .OrderByDescending(c => c.IssuedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
