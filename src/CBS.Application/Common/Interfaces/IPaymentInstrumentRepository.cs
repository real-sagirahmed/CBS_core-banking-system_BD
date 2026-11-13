// ARCHITECTURAL INTENT: Payment Instrument Repository Interface
// adhering to Bangladesh Bank BRPD Circular No. 12 (1.5.6, 3.11, 3.12).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Enums;
using CBS.Domain.PaymentInstruments;

namespace CBS.Application.Common.Interfaces;

public interface IPaymentInstrumentRepository
{
    Task<PaymentInstrument?> GetByIdAsync(string instrumentId, CancellationToken cancellationToken = default);
    Task<PaymentInstrument?> GetByInstrumentNumberAsync(string instrumentNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentInstrument>> GetOutstandingInstrumentsAsync(string? branchCode, PaymentInstrumentType? type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentInstrument>> GetUnpresentedInstrumentsAsync(DateOnly asOfDate, int minimumDaysUnpresented, CancellationToken cancellationToken = default);
    Task AddAsync(PaymentInstrument instrument, CancellationToken cancellationToken = default);
    Task UpdateAsync(PaymentInstrument instrument, CancellationToken cancellationToken = default);
}
