using CBS.Application.Common.Interfaces;
using CBS.Domain.Cheques;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfChequeBookRepository : IChequeBookRepository
{
    private readonly CbsDbContext _dbContext;

    public EfChequeBookRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ChequeBook?> GetBookByIdAsync(string bookId, CancellationToken ct = default)
    {
        return await _dbContext.ChequeBooks
            .Include(b => b.Leaves)
            .FirstOrDefaultAsync(b => b.Id == bookId, ct);
    }

    public async Task<IEnumerable<ChequeBook>> GetBooksByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(accountNumber);
        return await _dbContext.ChequeBooks
            .Include(b => b.Leaves)
            .Where(b => b.AccountNumber == acc)
            .ToListAsync(ct);
    }

    public async Task<ChequeLeaf?> GetLeafAsync(string accountNumber, long leafNumber, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(accountNumber);
        return await _dbContext.ChequeLeaves
            .FirstOrDefaultAsync(l => l.AccountNumber == acc && l.LeafNumber == leafNumber, ct);
    }

    public async Task AddBookAsync(ChequeBook book, CancellationToken ct = default)
    {
        await _dbContext.ChequeBooks.AddAsync(book, ct);
    }

    public Task UpdateBookAsync(ChequeBook book, CancellationToken ct = default)
    {
        _dbContext.ChequeBooks.Update(book);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<ChequeBook>> GetUndeliveredBooksAsync(DateOnly asOfDate, CancellationToken ct = default)
    {
        return await _dbContext.ChequeBooks
            .Where(b => b.Status != ChequeBookStatus.DeliveredToCustomer && b.Status != ChequeBookStatus.Destroyed)
            .ToListAsync(ct);
    }
}
