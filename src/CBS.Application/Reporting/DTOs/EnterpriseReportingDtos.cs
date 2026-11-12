using System;
using System.Collections.Generic;

namespace CBS.Application.Reporting.DTOs;

// --- 1. Remittance Cash Incentive DTOs ---

public sealed record DisburseRemittanceIncentiveRequest(
    string RemittanceReferenceNumber,
    string ExchangeHouseCode,
    string RemitterName,
    string RemitterCountry,
    string BeneficiaryAccountNumber,
    string BeneficiaryCif,
    decimal RemittanceAmountBdt,
    string DisbursingBranchCode,
    string DisbursedByUserId);

public sealed record RemittanceIncentiveResponseDto(
    string IncentiveId,
    string RemittanceReferenceNumber,
    string ExchangeHouseCode,
    string RemitterName,
    string RemitterCountry,
    string BeneficiaryAccountNumber,
    string BeneficiaryCif,
    decimal RemittanceAmountBdt,
    decimal IncentiveRate,
    decimal IncentiveAmountBdt,
    decimal TotalDisbursedBdt,
    string DisbursingBranchCode,
    DateTime DisbursedAtUtc,
    string DisbursedByUserId,
    string ClaimStatus,
    string? ClaimBatchReference,
    DateTime? ClaimSubmittedAtUtc,
    DateTime? ReimbursedAtUtc,
    string? CentralBankVoucherReference);

public sealed record SubmitRemittanceIncentiveBatchClaimRequest(
    List<string> IncentiveIds,
    string ClaimBatchReference,
    string SubmitterUserId);

public sealed record RecordCentralBankReimbursementRequest(
    string CentralBankVoucherReference,
    string RecordedByUserId);


// --- 2. Daily Clean Cash DTOs ---

public sealed record GenerateDailyCleanCashRequest(
    string BranchCode,
    DateOnly OperatingDate,
    decimal OpeningVaultCash,
    decimal OpeningTellerCash,
    decimal TotalCashReceived,
    decimal TotalCashPaid,
    decimal PhysicalClosingCash);

public sealed record CertifyDailyCleanCashRequest(
    string HeadCashierUserId,
    string ManagerUserId);

public sealed record DailyCleanCashResponseDto(
    string SheetId,
    string BranchCode,
    DateOnly OperatingDate,
    decimal OpeningVaultCash,
    decimal OpeningTellerCash,
    decimal TotalCashReceived,
    decimal TotalCashPaid,
    decimal CalculatedClosingCash,
    decimal PhysicalClosingCash,
    decimal CashDifference,
    string ProofStatus,
    string? DiscrepancyNotes,
    string? CertifiedByHeadCashierUserId,
    string? ApprovedByManagerUserId,
    DateTime? CertifiedAtUtc,
    DateTime CreatedAtUtc);


// --- 3. Large Loan & Statutory Portfolio DTOs ---

public sealed record LargeLoanBorrowerItemDto(
    string CifNumber,
    string BorrowerName,
    decimal FundedExposureBdt,
    decimal NonFundedExposureBdt,
    decimal TotalExposureBdt,
    decimal PercentageOfCapitalBase);

public sealed record LargeLoanPortfolioResponseDto(
    decimal CapitalBaseBdt,
    decimal LargeLoanThresholdBdt,
    int TotalLargeLoansCount,
    decimal TotalLargeLoanExposureBdt,
    IReadOnlyList<LargeLoanBorrowerItemDto> Items);

public sealed record NplClassificationSummaryDto(
    DateOnly AsOfDate,
    int TotalLoansCount,
    decimal TotalOutstandingBdt,
    decimal StandardAmountBdt,
    decimal SmaAmountBdt,
    decimal SubStandardAmountBdt,
    decimal DoubtfulAmountBdt,
    decimal BadLossAmountBdt,
    decimal TotalNplAmountBdt,
    decimal NplRatioPercentage,
    decimal TotalRequiredProvisionBdt);
