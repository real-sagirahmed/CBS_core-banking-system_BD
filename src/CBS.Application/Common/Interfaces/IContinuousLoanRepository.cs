// ARCHITECTURAL INTENT: Repository Contract for Continuous Loan (CC/OD) Facilities
// enforcing BRPD Circular 12 (4.1, 3.9) query patterns for single facility lookup, DP variance,
// expired unrenewed facilities, and overdue stock statement monitoring.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Lending.Continuous;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface IContinuousLoanRepository
{
    Task<ContinuousLoanFacility?> GetByAccountNumberAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContinuousLoanFacility>> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContinuousLoanFacility>> GetDrawingPowerDeficitFacilitiesAsync(string? branchCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContinuousLoanFacility>> GetExpiredUnrenewedFacilitiesAsync(DateOnly asOfDate, string? branchCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContinuousLoanFacility>> GetOverdueStockStatementFacilitiesAsync(DateOnly asOfDate, int thresholdDays, string? branchCode, CancellationToken cancellationToken = default);

    Task AddAsync(ContinuousLoanFacility facility, CancellationToken cancellationToken = default);
    Task UpdateAsync(ContinuousLoanFacility facility, CancellationToken cancellationToken = default);

    Task AddStockStatementAsync(ContinuousLoanStockStatement statement, CancellationToken cancellationToken = default);
    Task AddGodownInspectionAsync(ContinuousLoanGodownInspection inspection, CancellationToken cancellationToken = default);
    Task AddTodRecordAsync(TemporaryOverdraftRecord todRecord, CancellationToken cancellationToken = default);
    Task AddRenewalRecordAsync(ContinuousLoanRenewalRecord renewalRecord, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContinuousLoanStockStatement>> GetStockStatementsByAccountAsync(string loanAccountNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContinuousLoanGodownInspection>> GetGodownInspectionsByAccountAsync(string loanAccountNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TemporaryOverdraftRecord>> GetTodRecordsByAccountAsync(string loanAccountNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContinuousLoanRenewalRecord>> GetRenewalRecordsByAccountAsync(string loanAccountNumber, CancellationToken cancellationToken = default);
}
