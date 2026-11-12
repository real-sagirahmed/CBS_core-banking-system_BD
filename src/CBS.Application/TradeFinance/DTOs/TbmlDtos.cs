using CBS.Domain.Enums;

namespace CBS.Application.TradeFinance.DTOs;

public record CreateOrUpdateTtpRequest(
    string CustomerCif,
    decimal ApprovedAnnualImportVolume,
    decimal ApprovedAnnualExportVolume,
    string Currency = "USD",
    string PermittedCountriesCsv = "",
    string PermittedHsPrefixesCsv = "",
    string RiskCategory = "Medium",
    DateOnly EffectiveFromDate = default,
    DateOnly ExpiryDate = default,
    string ApprovedByUserId = "COMPLIANCE_HEAD01");

public record TradeTransactionProfileDto(
    Guid Id,
    string CustomerCif,
    decimal ApprovedAnnualImportVolume,
    decimal ApprovedAnnualExportVolume,
    decimal CumulativeYearToDateImportVolume,
    decimal CumulativeYearToDateExportVolume,
    string Currency,
    string PermittedCountriesCsv,
    string PermittedHsPrefixesCsv,
    string RiskCategory,
    DateOnly EffectiveFromDate,
    DateOnly ExpiryDate,
    DateTime LastUpdatedAtUtc,
    string ApprovedByUserId);

public record VerifyPriceRequest(
    string HsCode,
    decimal DeclaredUnitPrice,
    string Currency = "USD",
    string UnitOfMeasure = "KG",
    bool IsExport = false);

public record PriceVerificationReportDto(
    string HsCode,
    decimal DeclaredUnitPrice,
    decimal BenchmarkMinPrice,
    decimal BenchmarkMaxPrice,
    decimal VariancePercentage,
    bool HasVarianceAnomaly,
    string? AnomalyType,
    string Narrative,
    DateTime VerifiedAtUtc);

public record ScreenTbmlRequest(
    string ReferenceNumber,
    string CustomerCif,
    decimal Amount,
    string Currency = "USD",
    decimal DeclaredUnitPrice = 0m,
    string HsCode = "",
    string PortOfLoading = "",
    string PortOfDischarge = "",
    string CounterpartyCountry = "",
    string VesselName = "",
    string VesselImo = "",
    bool IsExport = false);

public record TbmlAlertDto(
    Guid AlertId,
    string ReferenceNumber,
    string CustomerCif,
    string AlertType,
    string Severity,
    string Details,
    bool IsEscalatedToCamlco,
    string? StrSarReference,
    string Status,
    DateTime CreatedAtUtc);

public record TbmlScreeningReportDto(
    string ReferenceNumber,
    string CustomerCif,
    bool IsPassed,
    string RiskRating,
    List<string> Anomalies,
    List<TbmlAlertDto> GeneratedAlerts,
    bool RequiresCamlcoEscalation,
    DateTime ScreenedAtUtc);

public record EscalateTbmlAlertRequest(
    string OfficerUserId = "CAMLCO_OFFICER01",
    string StrSarReference = "",
    string Remarks = "Suspicious transaction escalated for BFIU filing");

public record TrackVesselRequest(
    string VesselName,
    string ImoNumber,
    string FlagCountry,
    string LastReportedPort,
    string DestinationPort,
    string LoggedByUserId = "OPS_OFFICER01");

public record VesselTrackingDto(
    Guid Id,
    string VesselName,
    string ImoNumber,
    string FlagCountry,
    string LastReportedPort,
    string DestinationPort,
    bool IsSanctionedOrBlacklisted,
    DateTime TrackedAtUtc,
    string LoggedByUserId);
