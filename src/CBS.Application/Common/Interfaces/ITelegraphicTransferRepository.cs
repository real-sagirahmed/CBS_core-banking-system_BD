// ARCHITECTURAL INTENT: Telegraphic Transfer Repository Interface
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 3 (3.13) and Module 1 (1.6.5).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Enums;
using CBS.Domain.TelegraphicTransfers;

namespace CBS.Application.Common.Interfaces;

public interface ITelegraphicTransferRepository
{
    Task<TelegraphicTransfer?> GetByIdAsync(string ttId, CancellationToken cancellationToken = default);
    Task<TelegraphicTransfer?> GetByTtNumberAsync(string ttNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelegraphicTransfer>> GetOutstandingTtAsync(string? branchCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelegraphicTransfer>> GetUnrespondedTtAgingAsync(DateOnly asOfDate, int minimumDaysUnresponded, CancellationToken cancellationToken = default);
    Task AddAsync(TelegraphicTransfer transfer, CancellationToken cancellationToken = default);
    Task UpdateAsync(TelegraphicTransfer transfer, CancellationToken cancellationToken = default);
}
