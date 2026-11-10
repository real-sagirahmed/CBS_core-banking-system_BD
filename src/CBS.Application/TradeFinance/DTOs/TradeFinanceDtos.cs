using CBS.Domain.Enums;

namespace CBS.Application.TradeFinance.DTOs;

public sealed record DraftDocumentaryCreditRequest(
    string LcNumber,
    LcType Type,
    Guid ApplicantId,
    string BeneficiaryName,
    string BeneficiaryAddress,
    string BeneficiaryCountry,
    string AdvisingBankBic,
    string AdvisingBankName,
    decimal Amount,
    string Currency,
    decimal CashMarginAmount,
    decimal TolerancePercentage,
    DateTime IssueDate,
    DateTime ExpiryDate,
    string ExpiryPlace,
    DateTime LatestShipmentDate,
    string PortOfLoading,
    string PortOfDischarge,
    string GoodsDescription,
    string HsCode,
    Incoterm Incoterm,
    string IrcNumber,
    bool IsIrcValid,
    bool IsCibDefaulter,
    string MakerUserId,
    decimal DeclaredUnitPrice,
    decimal BenchmarkMinPrice,
    decimal BenchmarkMaxPrice,
    bool IsSanctionHit = false,
    bool IsDualUseHsCode = false,
    string BranchCode = "001",
    string? CustomerDepositAccountNumber = null,
    string? MasterLcNumber = null,
    decimal? MasterLcAmount = null,
    DateTime? MasterLcShipmentDate = null,
    DateTime? MasterLcExpiryDate = null,
    bool EdfRefinanced = false);

public sealed record AuthorizeDocumentaryCreditRequest(
    string LcNumber,
    string CheckerUserId,
    string IssuingBankBic = "S7BDBD2X");

public sealed record DocumentaryCreditResponse(
    string LcNumber,
    string Type,
    Guid ApplicantId,
    string BeneficiaryName,
    string BeneficiaryCountry,
    decimal Amount,
    string Currency,
    decimal CashMarginAmount,
    string Status,
    string? CheckerUserId,
    string? RawSwiftMt700,
    string? TbmlRiskRating,
    string? TbmlRiskSummary);

public sealed record IssueShippingGuaranteeRequest(
    string SgReference,
    string LcNumber,
    Guid ApplicantId,
    string CarrierName,
    string VesselName,
    string PortOfDischarge,
    decimal GuaranteeAmount,
    string Currency,
    decimal CashMarginAmount,
    string MakerUserId,
    string BranchCode = "001",
    string? CustomerDepositAccountNumber = null);

public sealed record AuthorizeShippingGuaranteeRequest(
    string SgReference,
    string CheckerUserId);

public sealed record ReleaseShippingGuaranteeRequest(
    string SgReference,
    string OriginalBlNumber,
    string ReleasedByUserId);

public sealed record ShippingGuaranteeResponse(
    string SgReference,
    string LcNumber,
    Guid ApplicantId,
    string CarrierName,
    string VesselName,
    decimal GuaranteeAmount,
    string Currency,
    decimal CashMarginAmount,
    string Status,
    string? CheckerUserId,
    string? SurrenderedOriginalBlNumber);

public sealed record RegisterExpRequest(
    string ExpNumber,
    string ExportLcOrContractNumber,
    Guid ExporterCustomerId,
    string DestinationCountry,
    decimal InvoiceAmount,
    string Currency,
    DateTime ShipmentDate,
    string MakerUserId,
    string BranchCode = "001");

public sealed record RealizeExpRequest(
    string ExpNumber,
    decimal ReceivedAmount,
    string Currency);

public sealed record EvaluateExpOverdueRequest(
    string ExpNumber,
    DateTime EvaluationDate);

public sealed record ExpRegulatoryResponse(
    string ExpNumber,
    string ExportLcOrContractNumber,
    Guid ExporterCustomerId,
    string DestinationCountry,
    decimal InvoiceAmount,
    decimal RealizedAmount,
    string Currency,
    DateTime ShipmentDate,
    DateTime RealizationDeadlineDate,
    string Status,
    bool ReportedToBangladeshBank);

public sealed record TbmlScreeningRequest(
    bool EntitySanctionHit,
    decimal DeclaredUnitPrice,
    decimal BenchmarkMinPrice,
    decimal BenchmarkMaxPrice,
    string PortOfLoading,
    string PortOfDischarge,
    bool IsDualUseHsCode);

public sealed record TbmlScreeningResponse(
    bool HasSanctionHit,
    bool HasPriceVarianceAnomaly,
    bool HasHighRiskPortRoute,
    bool HasDualUseGoodsRisk,
    decimal UnitPriceVariancePercentage,
    string RiskRating,
    string RiskSummary);

public sealed record ExpEodBatchRequest(
    string BranchCode,
    string? EvaluationDate = null);

public sealed record ExpEodBatchResponseDto(
    string BranchCode,
    int TotalProcessed,
    int OverdueCount,
    decimal TotalOverdueAmount,
    string Message);

public sealed record OverdueExpReportDto(
    string ExpNumber,
    string ExportLcNumber,
    Guid ExporterCustomerId,
    string DestinationCountry,
    decimal InvoiceAmount,
    decimal RealizedAmount,
    string Currency,
    DateTime ShipmentDate,
    DateTime RealizationDeadlineDate,
    int DaysOverdue,
    string BranchCode);

public sealed record TbmlAuditRecordDto(
    string LcNumber,
    string ApplicantInfo,
    string BeneficiaryInfo,
    decimal DeclaredPrice,
    decimal BenchmarkMin,
    decimal BenchmarkMax,
    decimal VariancePercentage,
    string RiskRating,
    string RiskSummary,
    DateTime IssueDate);
