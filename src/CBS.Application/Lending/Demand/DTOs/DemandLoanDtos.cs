using CBS.Domain.Enums;

namespace CBS.Application.Lending.Demand.DTOs;

public record InitiateDemandLoanRequest(
    string LoanAccountNumber,
    string CifNumber,
    DemandLoanType Type,
    string ProductCode,
    string LoanReferenceNumber,
    string MisEconomicSectorCode,
    string MisIndustryCode,
    string BranchCode,
    decimal SanctionLimit,
    decimal AnnualInterestRate,
    decimal PenalInterestRate,
    DateOnly SanctionDate,
    DateOnly MaturityDate,
    RepaymentPriorityScheme RepaymentPriority = RepaymentPriorityScheme.Standard);

public record DisburseDemandLoanRequest(
    string LoanAccountNumber,
    decimal Amount,
    DateOnly DisbursementDate,
    string? Remarks = null);

public record AccrueDailyInterestRequest(
    string LoanAccountNumber,
    DateOnly AccrualDate,
    bool IsOverdueOrClassified = false);

public record CapitalizeInterestRequest(
    string LoanAccountNumber,
    string ApprovalReference,
    DateOnly CapitalizationDate);

public record RepayDemandLoanRequest(
    string LoanAccountNumber,
    decimal Amount,
    RepaymentSourceType Source,
    DateOnly PaymentDate);

public record ForcedPadRequest(
    string LoanAccountNumber,
    string CifNumber,
    string BranchCode,
    string ImportLcNumber,
    string BillDocumentNumber,
    decimal BillAmount,
    decimal AnnualInterestRate,
    decimal PenalInterestRate,
    DateOnly ConversionDate,
    DateOnly MaturityDate,
    string MisSector = "SEC-TRADE-IMP",
    string MisIndustry = "IND-COMMERCIAL");

public record DemandLoanResponse(
    string LoanAccountNumber,
    string CifNumber,
    string Type,
    string ProductCode,
    string LoanReferenceNumber,
    string MisEconomicSectorCode,
    string MisIndustryCode,
    string BranchCode,
    decimal SanctionLimit,
    decimal OutstandingPrincipal,
    decimal RegularAccruedInterest,
    decimal OverdueInterest,
    decimal PenalInterest,
    decimal AccruedCharges,
    decimal InterestSuspense,
    decimal AnnualInterestRate,
    decimal PenalInterestRate,
    DateOnly SanctionDate,
    DateOnly MaturityDate,
    DateOnly? DisbursedDate,
    string Status,
    string RepaymentPriority,
    DateOnly? CapitalizedDate,
    string? CapitalizationApprovalReference);
