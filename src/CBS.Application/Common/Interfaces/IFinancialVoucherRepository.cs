using CBS.Application.GeneralLedger.DTOs;
using CBS.Domain.GeneralLedger;

namespace CBS.Application.Common.Interfaces;

public interface IFinancialVoucherRepository
{
    Task AddAsync(FinancialVoucher voucher, CancellationToken cancellationToken = default);
    Task<FinancialVoucher?> GetByVoucherNumberAsync(string voucherNumber, CancellationToken cancellationToken = default);
    Task<FinancialVoucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialVoucher>> GetByDateRangeAsync(DateOnly fromDate, DateOnly toDate, string? branchSol = null, CancellationToken cancellationToken = default);
    Task<TrialBalanceDto> GenerateTrialBalanceAsync(DateOnly asOfDate, string? branchSol = null, CancellationToken cancellationToken = default);
}
