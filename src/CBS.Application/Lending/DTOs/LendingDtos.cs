using CBS.Domain.Enums;

namespace CBS.Application.Lending.DTOs;

public record CreateLoanAccountRequest(
    string CifNumber,
    LoanFacilityType FacilityType,
    string BranchCode,
    decimal SanctionLimit,
    decimal DrawingPower,
    decimal InterestRate,
    DateOnly MaturityDate);

public record LoanAccountResponseDto(
    string AccountNumber,
    string CifNumber,
    LoanFacilityType FacilityType,
    string BranchCode,
    decimal SanctionLimit,
    decimal DrawingPower,
    decimal OutstandingPrincipal,
    decimal RegularAccruedInterest,
    decimal OverdueInterest,
    decimal PenalInterest,
    decimal AccruedCharges,
    decimal InterestSuspense,
    decimal InterestRate,
    bool IsCadVetted,
    string? CadVettedByUserId,
    DateOnly SanctionDate,
    DateOnly MaturityDate,
    DateOnly? DisbursedDate,
    string? DisbursedByUserId,
    string? DisbursementBeneficiarySupplier,
    LoanClassificationGrade Classification,
    int DaysPastDue,
    bool IsShariahCompliant);

public record VetCadChecklistRequest(string AccountNumber);

public record DisburseLoanRequest(
    string AccountNumber,
    decimal Amount,
    string? SupplierBeneficiary = null);

public record ProcessLoanRepaymentRequest(
    string AccountNumber,
    decimal RepaymentAmount);

public record RepaymentResultResponseDto(
    string AccountNumber,
    decimal TotalPaid,
    decimal AllocatedCharges,
    decimal AllocatedPenalInterest,
    decimal AllocatedOverdueInterest,
    decimal AllocatedRegularInterest,
    decimal AllocatedPrincipal,
    decimal UnallocatedSurplus,
    decimal RemainingOutstandingPrincipal);

public record EvaluateLoanClassificationRequest(
    string AccountNumber,
    int DaysPastDue);
