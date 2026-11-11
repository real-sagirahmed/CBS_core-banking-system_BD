using CBS.Domain.Enums;
using CBS.Domain.Reporting.ValueObjects;

namespace CBS.Application.Reporting.DTOs;

// Daily Clean Cash (BRPD 11.1.1-a)
public sealed record DailyCleanCashReportDto(
    string BranchCode,
    DateOnly ReportDate,
    decimal OpeningVaultCash,
    decimal OpeningTellerCash,
    decimal TotalCashReceipts,
    decimal TotalCashDisbursements,
    decimal ClosingTellerCash,
    decimal ClosingVaultCash,
    decimal TotalClosingCashInHand,
    IReadOnlyList<DailyCleanCashSummaryRow> BranchSummaries);

// Daily Affairs (BRPD 11.1.1-b)
public sealed record DailyAffairsBalanceItem(
    string GlAccountCode,
    string GlAccountName,
    string Category, // Asset, Liability, Equity, Income, Expense
    decimal DebitBalance,
    decimal CreditBalance);

public sealed record DailyAffairsReportDto(
    string BranchCode,
    DateOnly ReportDate,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalDeposits,
    decimal TotalAdvances,
    decimal CashInVault,
    decimal BalanceWithBangladeshBank,
    decimal TotalIncomeToday,
    decimal TotalExpenseToday,
    decimal NetProfitOrLossToday,
    IReadOnlyList<DailyAffairsBalanceItem> BalanceSheetItems);

// Deposit Analytics & Slabs (BRPD 11.1.2)
public sealed record DepositAnalyticsReportDto(
    DateOnly AsOfDate,
    int TotalDepositAccounts,
    decimal TotalDepositVolume,
    decimal CasaDepositRatioPercentage,
    IReadOnlyList<DepositSlabSummaryRow> Slabs,
    IReadOnlyList<DepositMixSummaryRow> MixBreakdowns);

// Unclaimed Deposits (BRPD 11.1.2-y & BCA §35)
public sealed record UnclaimedDepositCandidateDto(
    string AccountNumber,
    string CustomerCif,
    string CustomerName,
    DepositProductType AccountType,
    decimal Balance,
    string Currency,
    DateOnly LastTransactionDate,
    decimal UnclaimedYears);

public sealed record ScanUnclaimedDepositsResponseDto(
    int TotalIdentifiedCount,
    decimal TotalIdentifiedAmount,
    IReadOnlyList<UnclaimedDepositCandidateDto> Candidates);

public sealed record IssueNoticeRequestDto(
    string AccountNumber,
    string NoticeReference,
    DateOnly NoticeDate);

public sealed record TransferUnclaimedDepositsRequestDto(
    IReadOnlyList<string> AccountNumbers,
    string TransferScheduleId,
    string CentralBankReceiptRef,
    DateOnly RemittanceDate,
    string AuthorizedByUserId);

public sealed record TransferUnclaimedDepositsResponseDto(
    string TransferScheduleId,
    int AccountsTransferredCount,
    decimal TotalAmountTransferred,
    string CentralBankReceiptRef,
    DateOnly RemittanceDate,
    IReadOnlyList<string> TransferredAccountNumbers);

// SBS-3 Loans Classification & Provision (BRPD 11.1.3)
public sealed record Sbs3ClassificationSummaryDto(
    int TotalLoanAccounts,
    decimal TotalOutstandingLoans,
    decimal TotalUnclassifiedLoans,
    decimal TotalClassifiedLoans,
    decimal ClassifiedLoanRatioPercentage, // NPL %
    decimal TotalRequiredGeneralProvision,
    decimal TotalRequiredSpecificProvision,
    decimal TotalRequiredProvision,
    decimal TotalMaintainedProvision,
    decimal TotalInterestSuspense);

public sealed record Sbs3ClassificationReportDto(
    DateOnly AsOfDate,
    Sbs3ClassificationSummaryDto Summary,
    IReadOnlyList<Sbs3ClassificationRow> Loans);

// Foreign Trade Summary (BRPD 11.1.4)
public sealed record TradeFinanceSummaryRow(
    string InstrumentType, // LC, PAD, BankGuarantee, ExportBill
    int ActiveCount,
    decimal TotalOutstandingAmount,
    decimal MarginHeldAmount,
    decimal OverdueAmount);

public sealed record ForeignTradeSummaryReportDto(
    DateOnly AsOfDate,
    int TotalActiveLcCount,
    decimal TotalLcLiabilityAmount,
    decimal TotalPadOutstandingAmount,
    decimal TotalGuaranteeLiabilityAmount,
    IReadOnlyList<TradeFinanceSummaryRow> Breakdown);

// Remittance Cash Incentive 2.5% (BRPD 11.1.5)
public sealed record RemittanceIncentiveItemDto(
    string TransactionReference,
    string RemitterName,
    string BeneficiaryName,
    string SendingCountry,
    string ExchangeHouse,
    decimal RemittanceAmountBdt,
    decimal IncentiveRatePercentage,
    decimal IncentiveAmountBdt,
    DateOnly DisbursedDate,
    bool IsClaimedFromCentralBank);

public sealed record RemittanceIncentiveReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalRemittanceCount,
    decimal TotalRemittedAmountBdt,
    decimal TotalCashIncentiveDisbursedBdt,
    IReadOnlyList<RemittanceIncentiveItemDto> Transactions);

// ADC Channel Reconciliation (BRPD 11.1.6)
public sealed record AdcChannelSummaryItemDto(
    string ChannelType, // ATM, POS, NPSB, BEFTN, RTGS, MFS
    int TotalTransactions,
    decimal TotalVolumeBdt,
    int SettledCount,
    decimal SettledVolumeBdt,
    int DisputedCount,
    decimal DisputedVolumeBdt);

public sealed record AdcReconciliationReportDto(
    DateOnly ReportDate,
    int TotalAdcTransactions,
    decimal TotalVolumeBdt,
    int TotalDisputes,
    decimal TotalDisputedAmountBdt,
    IReadOnlyList<AdcChannelSummaryItemDto> Channels);

// Statutory Archive DTOs (BRPD 11.2)
public sealed record SealReportRequestDto(
    RegulatoryReportType ReportType,
    DateOnly ReportingCutoffDate,
    string? BranchId,
    string GeneratedByUserId,
    ReportExportFormat ExportFormat,
    string SerializedPayload,
    int RecordCount);

public sealed record PreservedReportSummaryDto(
    Guid Id,
    RegulatoryReportType ReportType,
    DateOnly ReportingCutoffDate,
    string? BranchId,
    DateTime GenerationTimestamp,
    string GeneratedByUserId,
    ReportExportFormat ExportFormat,
    string ContentHash,
    int RecordCount,
    bool IsSubmitted,
    string? SubmissionReference,
    DateTime? SubmissionTimestamp,
    bool IsIntegrityVerified);

public sealed record SubmitReportRequestDto(
    string SubmissionReference,
    string SubmittedByUserId);
