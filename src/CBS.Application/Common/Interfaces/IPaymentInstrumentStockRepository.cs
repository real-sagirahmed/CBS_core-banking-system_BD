// ARCHITECTURAL INTENT: Payment Instrument Stock Book Repository Interface
// adhering to Bangladesh Bank BRPD Circular No. 12 (1.5.6-ii, 3.11.1-iii..iv).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Enums;
using CBS.Domain.PaymentInstruments;

namespace CBS.Application.Common.Interfaces;

public interface IPaymentInstrumentStockRepository
{
    Task<PaymentInstrumentStockBook?> GetByIdAsync(string bookId, CancellationToken cancellationToken = default);
    Task<PaymentInstrumentStockBook?> GetActiveBookForBranchAsync(string branchCode, PaymentInstrumentType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentInstrumentStockBook>> GetStockBooksByBranchAsync(string branchCode, CancellationToken cancellationToken = default);
    Task AddAsync(PaymentInstrumentStockBook stockBook, CancellationToken cancellationToken = default);
    Task UpdateAsync(PaymentInstrumentStockBook stockBook, CancellationToken cancellationToken = default);
}
