using System;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Reporting;

namespace CBS.Application.Common.Interfaces;

public interface IDailyEodBalancingRepository
{
    Task<DailyEodBalancingSheet?> GetByIdAsync(string sheetId, CancellationToken ct = default);
    Task<DailyEodBalancingSheet?> GetByBranchAndDateAsync(string branchCode, DateOnly operatingDate, CancellationToken ct = default);
    Task AddAsync(DailyEodBalancingSheet sheet, CancellationToken ct = default);
    Task UpdateAsync(DailyEodBalancingSheet sheet, CancellationToken ct = default);
}
