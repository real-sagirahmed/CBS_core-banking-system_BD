using CBS.Domain.Enums;

namespace CBS.Application.TradeFinance.DTOs;

public record RegisterMasterExportLcRequest(
    string MasterLcNumber,
    MasterContractType ContractType,
    string ExporterCif,
    string IssuingBankBic,
    string IssuingBankName,
    string BuyerName,
    string BuyerCountry,
    decimal TotalAmount,
    string Currency = "USD",
    decimal ValueAdditionPercentage = 25.00m,
    DateOnly IssueDate = default,
    DateOnly ExpiryDate = default,
    DateOnly LatestShipmentDate = default,
    string BranchCode = "0101",
    string MakerUserId = "MAKER01",
    string? ReplacedContractRef = null);

public record AuthorizeMasterExportLcRequest(
    string CheckerUserId = "CHECKER01");

public record MasterExportLcResponseDto(
    string MasterLcNumber,
    string ContractType,
    string ExporterCif,
    string IssuingBankBic,
    string IssuingBankName,
    string BuyerName,
    string BuyerCountry,
    decimal TotalAmount,
    string Currency,
    decimal ValueAdditionPercentage,
    decimal MaxPermissibleB2bAmount,
    decimal CumulativeB2bAllocatedAmount,
    decimal RemainingB2bCapacity,
    DateOnly IssueDate,
    DateOnly ExpiryDate,
    DateOnly LatestShipmentDate,
    string Status,
    string BranchCode,
    string MakerUserId,
    string? CheckerUserId,
    DateTime? AuthorizedAtUtc,
    string? ReplacedContractRef);

public record OpenB2bLcUnderMasterRequest(
    string B2bLcNumber,
    decimal Amount,
    string Currency = "USD",
    decimal CashMarginAmount = 0m,
    DateOnly ShipmentDate = default,
    DateOnly ExpiryDate = default,
    string SupplierBeneficiaryName = "",
    string SupplierBeneficiaryCountry = "",
    string AdvisingBankBic = "",
    string AdvisingBankName = "",
    string ProformaInvoiceNumber = "",
    DateOnly ProformaInvoiceDate = default,
    string LcafNumber = "",
    string ImpFormNumber = "",
    string GoodsDescription = "",
    string HsCode = "",
    string MakerUserId = "MAKER01",
    string BranchCode = "0101");

public record B2bLcIssuedResponseDto(
    string B2bLcNumber,
    string MasterLcNumber,
    decimal Amount,
    string Currency,
    decimal CashMarginAmount,
    DateOnly ShipmentDate,
    DateOnly ExpiryDate,
    string ProformaInvoiceNumber,
    string LcafNumber,
    string ImpFormNumber,
    decimal RemainingMasterCapacity,
    DateTime IssuedAtUtc);

public record AmendMasterLcRequest(
    decimal NewTotalAmount,
    string MakerUserId = "MAKER01",
    string CheckerUserId = "CHECKER01");

public record CloseMasterLcRequest(
    string MakerUserId = "MAKER01",
    string CheckerUserId = "CHECKER01");

public record MasterLcHistoryReportDto(
    string MasterLcNumber,
    string ExporterCif,
    string Status,
    decimal MasterAmount,
    string Currency,
    decimal StatutoryCeilingAmount,
    decimal CumulativeB2bAllocatedAmount,
    decimal RemainingCapacity,
    int TotalB2bCount,
    List<B2bLcIssuedResponseDto> ActiveB2bLcs,
    DateTime GeneratedAtUtc);
