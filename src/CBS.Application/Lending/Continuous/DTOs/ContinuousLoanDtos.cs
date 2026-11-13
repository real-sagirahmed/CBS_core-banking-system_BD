// ARCHITECTURAL INTENT: Data Transfer Objects for Continuous Loan (CC/OD) Facilities
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 and 3.9.

using System;
using System.Collections.Generic;

namespace CBS.Application.Lending.Continuous.DTOs;

public record OpenContinuousLoanRequest(
    string CifNumber,
    string BranchCode,
    string Category,
    decimal SanctionLimit,
    decimal InitialDrawingPower,
    decimal RegularInterestRate,
    decimal? PenalInterestRate,
    DateOnly SanctionDate,
    DateOnly ExpiryDate,
    decimal? StockMarginPercent,
    decimal? BookDebtsMarginPercent,
    string? LinkedDepositAccountNumber);

public record ContinuousLoanResponseDto(
    string AccountNumber,
    string CifNumber,
    string BranchCode,
    string Category,
    string? LinkedDepositAccountNumber,
    decimal SanctionLimit,
    decimal DrawingPower,
    decimal OutstandingPrincipal,
    decimal AvailableDrawingLimit,
    decimal RegularAccruedInterest,
    decimal OverdueInterest,
    decimal PenalInterest,
    decimal AccruedCharges,
    decimal InterestSuspense,
    decimal RegularInterestRate,
    decimal PenalInterestRate,
    DateOnly SanctionDate,
    DateOnly ExpiryDate,
    DateOnly? LastRenewalDate,
    int RenewalCount,
    string RenewalStatus,
    decimal StockMarginPercent,
    decimal BookDebtsMarginPercent,
    decimal? CurrentTodAmount,
    DateOnly? CurrentTodExpiryDate,
    decimal? CurrentTodPenalRate,
    DateOnly? LastStockStatementDate,
    DateOnly? LastInspectionDate,
    string Classification,
    int DaysPastDue,
    bool IsDebitRestricted);

public record SubmitStockStatementRequest(
    DateOnly StatementDate,
    DateOnly AsOfPeriodDate,
    decimal RawMaterialsValue,
    decimal WorkInProgressValue,
    decimal FinishedGoodsValue,
    decimal CreditorsForGoods,
    decimal BookDebtsUnder90Days,
    decimal BookDebtsOver90Days,
    decimal StockMarginPercent,
    decimal BookDebtsMarginPercent,
    string SubmittedByBorrowerRepresentative,
    string VettedByOfficerUserId);

public record StockStatementResponseDto(
    string StatementId,
    string LoanAccountNumber,
    DateOnly StatementDate,
    DateOnly AsOfPeriodDate,
    decimal TotalStockValue,
    decimal PaidStockValue,
    decimal BookDebtsUnder90Days,
    decimal EligibleStockDrawingPower,
    decimal EligibleBookDebtsDrawingPower,
    decimal TotalCalculatedDrawingPower,
    string VettedByOfficerUserId,
    DateTime VettedAtUtc);

public record RecordGodownInspectionRequest(
    DateOnly InspectionDate,
    string InspectorUserId,
    string GodownLocation,
    decimal InspectedStockValue,
    string StockConditionDescription,
    string InsurancePolicyNumber,
    string InsuranceCompany,
    DateOnly InsuranceExpiryDate,
    decimal InsuranceSumInsured,
    string InspectionResult,
    string Remarks);

public record GodownInspectionResponseDto(
    string InspectionId,
    string LoanAccountNumber,
    DateOnly InspectionDate,
    string InspectorUserId,
    string GodownLocation,
    decimal InspectedStockValue,
    string InsurancePolicyNumber,
    DateOnly InsuranceExpiryDate,
    decimal InsuranceSumInsured,
    bool IsInsuranceValid,
    bool IsInsuranceAdequate,
    decimal InsurableCappedStockValue,
    string InspectionResult,
    string Remarks);

public record GrantTemporaryOverdraftRequest(
    decimal Amount,
    DateOnly ExpiryDate,
    decimal PenalRate,
    string ApprovedByUserId,
    string ApprovalReference,
    string Reason);

public record TemporaryOverdraftResponseDto(
    string TodId,
    string LoanAccountNumber,
    decimal SanctionedTodAmount,
    decimal RegularLimitAtGrant,
    decimal EffectiveLimit,
    DateOnly GrantDate,
    DateOnly ExpiryDate,
    decimal TodPenalInterestRate,
    string ApprovedByUserId,
    string ApprovalReference,
    string Reason,
    string Status);

public record ContinuousLoanWithdrawalRequest(
    decimal Amount,
    string ChannelOrVoucher,
    string ApprovedByUserId,
    DateOnly AsOfDate,
    bool IsOverrideApproved = false);

public record ContinuousLoanDepositRequest(
    decimal Amount);

public record QuarterlyCapitalizationRequest(
    DateOnly QuarterEndDate);

public record QuarterlyCapitalizationResponseDto(
    string AccountNumber,
    DateOnly QuarterEndDate,
    decimal CapitalizedInterestAmount,
    decimal CapitalizedPenalAmount,
    decimal NewOutstandingPrincipal);

public record RenewContinuousLoanRequest(
    decimal NewLimit,
    DateOnly NewExpiryDate,
    decimal NewInterestRate,
    string ApprovedByUserId,
    string SanctionAdviceReference,
    string Remarks);

public record ContinuousLoanRenewalResponseDto(
    string RenewalId,
    string LoanAccountNumber,
    DateOnly RenewalDate,
    decimal PreviousLimit,
    decimal NewLimit,
    DateOnly PreviousExpiryDate,
    DateOnly NewExpiryDate,
    decimal PreviousInterestRate,
    decimal NewInterestRate,
    string ApprovedByUserId,
    string SanctionAdviceReference,
    string Remarks);

public record DrawingPowerVarianceItemDto(
    string AccountNumber,
    string CifNumber,
    string BranchCode,
    decimal SanctionLimit,
    decimal DrawingPower,
    decimal OutstandingPrincipal,
    decimal DeficitAmount);

public record DrawingPowerVarianceReportDto(
    DateOnly AsOfDate,
    string? BranchCode,
    int TotalAccountsWithDeficit,
    decimal TotalDeficitAmount,
    IReadOnlyList<DrawingPowerVarianceItemDto> DeficitAccounts);

public record ExpiredContinuousLoanItemDto(
    string AccountNumber,
    string CifNumber,
    string BranchCode,
    decimal SanctionLimit,
    decimal OutstandingPrincipal,
    DateOnly ExpiryDate,
    int DaysOverdue,
    decimal AccruedPenalInterest);

public record ExpiredContinuousLoanReportDto(
    DateOnly AsOfDate,
    string? BranchCode,
    int TotalExpiredAccounts,
    decimal TotalExpiredOutstanding,
    IReadOnlyList<ExpiredContinuousLoanItemDto> ExpiredAccounts);

public record OverdueStockStatementItemDto(
    string AccountNumber,
    string CifNumber,
    string BranchCode,
    decimal SanctionLimit,
    decimal OutstandingPrincipal,
    DateOnly? LastStockStatementDate,
    int DaysSinceLastStatement);

public record OverdueStockStatementReportDto(
    DateOnly AsOfDate,
    int ThresholdDays,
    string? BranchCode,
    int TotalOverdueAccounts,
    IReadOnlyList<OverdueStockStatementItemDto> OverdueAccounts);
