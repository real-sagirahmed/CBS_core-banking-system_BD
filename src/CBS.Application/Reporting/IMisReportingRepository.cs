using CBS.Application.Reporting.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Reporting;

namespace CBS.Application.Reporting;

public interface IMisReportingRepository
{
    // Reporting Query Methods
    Task<DailyCleanCashReportDto> GetDailyCleanCashReportAsync(string? branchCode, DateOnly date, CancellationToken ct = default);
    Task<DailyAffairsReportDto> GetDailyAffairsReportAsync(string? branchCode, DateOnly date, CancellationToken ct = default);
    Task<DepositAnalyticsReportDto> GetDepositAnalyticsReportAsync(DateOnly asOfDate, CancellationToken ct = default);
    Task<List<UnclaimedDepositCandidateDto>> ScanUnclaimedDepositCandidatesAsync(DateOnly asOfDate, CancellationToken ct = default);
    Task<Sbs3ClassificationReportDto> GetSbs3ClassificationReportAsync(DateOnly asOfDate, CancellationToken ct = default);
    Task<ForeignTradeSummaryReportDto> GetForeignTradeSummaryReportAsync(DateOnly asOfDate, CancellationToken ct = default);
    Task<RemittanceIncentiveReportDto> GetRemittanceIncentiveReportAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task<AdcReconciliationReportDto> GetAdcReconciliationReportAsync(DateOnly date, CancellationToken ct = default);

    // Statutory Report Archive Methods
    Task SaveArchiveAsync(StatutoryReportArchive archive, CancellationToken ct = default);
    Task<StatutoryReportArchive?> GetArchiveByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StatutoryReportArchive>> GetArchivesAsync(RegulatoryReportType? type, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default);

    // Unclaimed Deposit Aggregate Methods
    Task SaveUnclaimedDepositItemAsync(UnclaimedDepositItem item, CancellationToken ct = default);
    Task<UnclaimedDepositItem?> GetUnclaimedDepositByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task<IReadOnlyList<UnclaimedDepositItem>> GetUnclaimedDepositsAsync(UnclaimedDepositStatus? status, CancellationToken ct = default);
    Task ExecuteCentralBankFundTransferAsync(string accountNumber, DateOnly date, CancellationToken ct = default);
}
