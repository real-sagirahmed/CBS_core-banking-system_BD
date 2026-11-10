using CBS.Domain.Cheques;

namespace CBS.Application.Common.Interfaces;

public interface IChequeBookRepository
{
    Task<ChequeBook?> GetBookByIdAsync(string bookId, CancellationToken ct = default);
    Task<IEnumerable<ChequeBook>> GetBooksByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task<ChequeLeaf?> GetLeafAsync(string accountNumber, long leafNumber, CancellationToken ct = default);
    Task AddBookAsync(ChequeBook book, CancellationToken ct = default);
    Task UpdateBookAsync(ChequeBook book, CancellationToken ct = default);
    Task<IEnumerable<ChequeBook>> GetUndeliveredBooksAsync(DateOnly asOfDate, CancellationToken ct = default);
}
