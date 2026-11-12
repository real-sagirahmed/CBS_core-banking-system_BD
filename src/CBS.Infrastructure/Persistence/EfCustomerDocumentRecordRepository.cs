using CBS.Application.Common.Interfaces;
using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfCustomerDocumentRecordRepository : ICustomerDocumentRecordRepository
{
    private readonly CbsDbContext _dbContext;

    public EfCustomerDocumentRecordRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(CustomerDocumentRecord document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document, nameof(document));
        await _dbContext.CustomerDocumentRecords.AddAsync(document, cancellationToken);
    }

    public async Task<CustomerDocumentRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CustomerDocumentRecords
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerDocumentRecord>> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CustomerDocumentRecords
            .AsNoTracking()
            .Where(d => d.CustomerCif == cif)
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerDocumentRecord>> GetExpiredComplianceDocumentsAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CustomerDocumentRecords
            .Where(d => d.ExpiryDate != null && d.ExpiryDate < asOfDate)
            .OrderBy(d => d.ExpiryDate)
            .ToListAsync(cancellationToken);
    }

    public Task UpdateAsync(CustomerDocumentRecord document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document, nameof(document));
        _dbContext.CustomerDocumentRecords.Update(document);
        return Task.CompletedTask;
    }
}
