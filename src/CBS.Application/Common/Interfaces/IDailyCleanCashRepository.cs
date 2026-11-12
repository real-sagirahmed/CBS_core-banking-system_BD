using System;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Reporting;

namespace CBS.Application.Common.Interfaces;

public interface IDailyCleanCashRepository
{
    Task<DailyCleanCashSheet?> GetByIdAsync(string sheetId, CancellationToken ct = default);
    Task<DailyCleanCashSheet?> GetByBranchAndDateAsync(string branchCode, DateOnly operatingDate, CancellationToken ct = default);
    Task AddAsync(DailyCleanCashSheet sheet, CancellationToken ct = default);
    Task UpdateAsync(DailyCleanCashSheet sheet, CancellationToken ct = default);
}
