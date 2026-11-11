using CBS.Domain.Enums;
using CBS.Domain.Shariah.ValueObjects;

namespace CBS.Application.Shariah.DTOs;

public record CreateMudarabaPoolRequestDto(
    string PoolName,
    MudarabaPoolType PoolType,
    string Currency,
    decimal MudaribShareRatio,
    decimal PerRatio = 2.0m,
    decimal IrrRatio = 1.0m);

public record ConfigureWeightageRequestDto(
    string ProductCode,
    string ProductName,
    decimal Weightage,
    decimal DeclaredProvisionalRate);

public record DistributePoolProfitRequestDto(
    decimal GrossInvestmentIncome);

public record MudarabaPoolDto(
    Guid Id,
    string PoolName,
    MudarabaPoolType PoolType,
    string Currency,
    decimal TotalDepositBalance,
    decimal MudaribShareRatio,
    decimal RabUlMalShareRatio,
    decimal PerRatio,
    decimal IrrRatio,
    string WeightagesJson,
    decimal AccumulatedPerBalance,
    decimal AccumulatedIrrBalance,
    DateTime CreatedAtUtc,
    DateTime? LastDistributionDateUtc);

public record PoolProfitDistributionResultDto(
    Guid PoolId,
    decimal GrossIncome,
    decimal MudaribSharePercentage,
    decimal MudaribShareAmount,
    decimal RabUlMalGrossShare,
    decimal PerDeductionAmount,
    decimal IrrDeductionAmount,
    decimal NetDistributableProfit,
    DateTime CalculatedAtUtc);

public record OpenAlWadiahAccountRequestDto(
    string AccountNumber,
    string Cif,
    string Currency,
    string SafeCustodyContractRef);

public record AlWadiahAccountDto(
    Guid Id,
    string AccountNumber,
    string Cif,
    string Currency,
    decimal Balance,
    decimal InterestRate,
    string SafeCustodyContractRef,
    bool IsActive,
    DateTime CreatedAtUtc);

public record CreateMurabahaFacilityRequestDto(
    string FacilityNumber,
    string Cif,
    string BranchCode,
    decimal PurchaseCost,
    decimal ProfitMarginAmount,
    int RepaymentTenorMonths,
    string GoodsDescription);

public record InspectGoodsRequestDto(
    string InspectionProofReference,
    string InspectorOfficerId);

public record DisburseMurabahaToVendorRequestDto(
    string VendorName,
    string VendorAccountNumber,
    string VendorBankRoutingNo,
    string InvoiceNumber,
    decimal InvoiceAmount,
    string AuthorizerOfficerId);

public record MurabahaFacilityDto(
    Guid Id,
    string FacilityNumber,
    string Cif,
    string BranchCode,
    decimal PurchaseCost,
    decimal ProfitMarginAmount,
    decimal TotalSalePrice,
    int RepaymentTenorMonths,
    decimal MonthlyInstallment,
    decimal OutstandingBalance,
    ShariahFacilityStatus Status,
    string GoodsDescription,
    string? InspectionProofReference,
    string? VendorDisbursementOrderJson,
    DateTime? DisbursedAtUtc,
    DateTime CreatedAtUtc);

public record CreateQuardFacilityRequestDto(
    string FacilityNumber,
    string Cif,
    decimal PrincipalAmount,
    decimal ActualAdministrativeFee,
    int TenorMonths);

public record QuardFacilityDto(
    Guid Id,
    string FacilityNumber,
    string Cif,
    decimal PrincipalAmount,
    decimal ActualAdministrativeFee,
    decimal InterestRate,
    decimal ProfitRate,
    decimal OutstandingBalance,
    int TenorMonths,
    bool IsSettled,
    DateTime CreatedAtUtc);

public record AccrueTaWidhPenaltyRequestDto(
    string FacilityReference,
    string CustomerCif,
    decimal OverduePenaltyAmount);

public record DisburseCharityFundRequestDto(
    string ApprovedSocialWelfareCause,
    string DisbursedByOfficerId);

public record TaWidhRecordDto(
    Guid Id,
    string FacilityReference,
    string CustomerCif,
    decimal OverduePenaltyAmount,
    DateTime AccruedAtUtc,
    TaWidhStatus Status,
    string? ApprovedSocialWelfareCause,
    DateTime? DisbursedAtUtc,
    string? DisbursedByOfficerId);

public record TaWidhFundSummaryDto(
    decimal TotalSuspenseBalance,
    int TotalAccruedCount,
    int TotalApprovedCount,
    int TotalDisbursedCount,
    IReadOnlyList<TaWidhRecordDto> Records);
