// ARCHITECTURAL INTENT: EF Core repository implementation for InlandBillPurchased (IBP)
// per BRPD Circular No. 12 Modules 2 & 4.

using CBS.Application.Common.Interfaces;
using CBS.Domain.Clearing;
using CBS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public sealed class EfInlandBillPurchasedRepository : IInlandBillPurchasedRepository
{
    private readonly CbsDbContext _dbContext;

    public EfInlandBillPurchasedRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InlandBillPurchased?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InlandBillsPurchased
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<InlandBillPurchased?> GetByReferenceNumberAsync(string referenceNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InlandBillsPurchased
            .FirstOrDefaultAsync(x => x.IbpReferenceNumber == referenceNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<InlandBillPurchased>> GetByCustomerAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InlandBillsPurchased
            .Where(x => x.CustomerAccountNumber == accountNumber)
            .OrderByDescending(x => x.PurchaseDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InlandBillPurchased>> GetByStatusAsync(InlandBillPurchasedStatus status, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InlandBillsPurchased
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.PurchaseDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(InlandBillPurchased ibp, CancellationToken cancellationToken = default)
    {
        await _dbContext.InlandBillsPurchased.AddAsync(ibp, cancellationToken);
    }

    public Task UpdateAsync(InlandBillPurchased ibp, CancellationToken cancellationToken = default)
    {
        _dbContext.InlandBillsPurchased.Update(ibp);
        return Task.CompletedTask;
    }
}
