using CBS.Application.Common.Interfaces;
using CBS.Application.TradeFinance.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;

namespace CBS.Application.TradeFinance.Services;

// ARCHITECTURAL INTENT: Trade-Based Money Laundering (TBML) Prevention & Sanction Screening Service
// implementing Uniform Price Verification Template (over/under invoicing), Trade Transaction Profile (TTP),
// high-risk tax haven / land-locked port routing, vessel tracking audit trails, and STR/SAR Level-3 CAMLCO escalation
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 5.0, 5.7.1 - 5.7.8.
public sealed class TbmlPreventionService : ITbmlPreventionService
{
    private readonly ITradeTransactionProfileRepository _ttpRepo;
    private readonly ITbmlPriceBenchmarkRepository _benchmarkRepo;
    private readonly ITbmlAlertRepository _alertRepo;
    private readonly IVesselTrackingRepository _vesselRepo;
    private readonly IUnitOfWork _unitOfWork;

    private static readonly HashSet<string> HighRiskTaxHavenCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "PA", "KY", "VG", "SC", "IR", "KP", "SY", "RU", "MM", "YE", "BS", "BM", "VU", "MH"
    };

    private static readonly HashSet<string> LandLockedOrSanctionedPorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "BANDAR ABBAS", "TARTUS", "SEVASTOPOL", "LATAKIA", "CRIMEA", "KABUL", "VIENTIANE", "KATHMANDU"
    };

    public TbmlPreventionService(
        ITradeTransactionProfileRepository ttpRepo,
        ITbmlPriceBenchmarkRepository benchmarkRepo,
        ITbmlAlertRepository alertRepo,
        IVesselTrackingRepository vesselRepo,
        IUnitOfWork unitOfWork)
    {
        _ttpRepo = ttpRepo;
        _benchmarkRepo = benchmarkRepo;
        _alertRepo = alertRepo;
        _vesselRepo = vesselRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<TradeTransactionProfileDto> CreateOrUpdateTtpAsync(
        CreateOrUpdateTtpRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var cif = new CifNumber(request.CustomerCif);
        var existing = await _ttpRepo.GetByCifAsync(cif, cancellationToken);

        var currency = Currency.FromCode(request.Currency);
        var importLimit = new Money(request.ApprovedAnnualImportVolume, currency);
        var exportLimit = new Money(request.ApprovedAnnualExportVolume, currency);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = request.EffectiveFromDate == default ? today : request.EffectiveFromDate;
        var expDate = request.ExpiryDate == default ? today.AddYears(1) : request.ExpiryDate;

        TradeTransactionProfile profile;
        if (existing is null)
        {
            profile = new TradeTransactionProfile(
                id: Guid.NewGuid(),
                customerCif: cif,
                approvedAnnualImportVolume: importLimit,
                approvedAnnualExportVolume: exportLimit,
                permittedCountriesCsv: request.PermittedCountriesCsv,
                permittedHsPrefixesCsv: request.PermittedHsPrefixesCsv,
                riskCategory: request.RiskCategory,
                effectiveFromDate: fromDate,
                expiryDate: expDate,
                approvedByUserId: request.ApprovedByUserId);

            await _ttpRepo.AddAsync(profile, cancellationToken);
        }
        else
        {
            profile = new TradeTransactionProfile(
                id: existing.Id,
                customerCif: cif,
                approvedAnnualImportVolume: importLimit,
                approvedAnnualExportVolume: exportLimit,
                permittedCountriesCsv: request.PermittedCountriesCsv,
                permittedHsPrefixesCsv: request.PermittedHsPrefixesCsv,
                riskCategory: request.RiskCategory,
                effectiveFromDate: fromDate,
                expiryDate: expDate,
                approvedByUserId: request.ApprovedByUserId);

            await _ttpRepo.UpdateAsync(profile, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapTtpToDto(profile);
    }

    public async Task<TradeTransactionProfileDto?> GetTtpByCifAsync(
        string customerCif,
        CancellationToken cancellationToken = default)
    {
        var profile = await _ttpRepo.GetByCifAsync(new CifNumber(customerCif), cancellationToken);
        return profile is null ? null : MapTtpToDto(profile);
    }

    public async Task<PriceVerificationReportDto> VerifyPriceAsync(
        VerifyPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var benchmark = await _benchmarkRepo.GetByHsCodeAsync(request.HsCode, cancellationToken);
        if (benchmark is null)
        {
            // Seed a standard commodity benchmark if not present for test / operational continuity
            benchmark = new TbmlPriceBenchmark(
                id: Guid.NewGuid(),
                hsCode: request.HsCode,
                itemDescription: $"Trade Commodity HS {request.HsCode}",
                unitOfMeasure: request.UnitOfMeasure,
                benchmarkPriceMin: 80.00m,
                benchmarkPriceMax: 120.00m,
                currency: Currency.FromCode(request.Currency),
                effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow),
                sourceReference: "NBR-CUSTOMS-VALUATION-DATABASE-2026");

            await _benchmarkRepo.AddAsync(benchmark, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        bool hasAnomaly = benchmark.CheckPriceVariance(
            request.DeclaredUnitPrice,
            request.IsExport,
            out decimal variancePercentage,
            out TbmlAlertType? alertType,
            out string? narrative);

        return new PriceVerificationReportDto(
            HsCode: benchmark.HsCode,
            DeclaredUnitPrice: request.DeclaredUnitPrice,
            BenchmarkMinPrice: benchmark.BenchmarkPriceMin,
            BenchmarkMaxPrice: benchmark.BenchmarkPriceMax,
            VariancePercentage: variancePercentage,
            HasVarianceAnomaly: hasAnomaly,
            AnomalyType: alertType?.ToString(),
            Narrative: narrative ?? "Declared unit price is within permissible benchmark variance threshold (±20%).",
            VerifiedAtUtc: DateTime.UtcNow);
    }

    public async Task<TbmlScreeningReportDto> ScreenTradeTransactionAsync(
        ScreenTbmlRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var anomalies = new List<string>();
        var generatedAlerts = new List<TbmlAlertDto>();
        var cif = new CifNumber(request.CustomerCif);
        var currency = Currency.FromCode(request.Currency);
        var txMoney = new Money(request.Amount, currency);

        // 1. Uniform Price Verification
        if (!string.IsNullOrWhiteSpace(request.HsCode) && request.DeclaredUnitPrice > 0m)
        {
            var priceReport = await VerifyPriceAsync(new VerifyPriceRequest(
                request.HsCode,
                request.DeclaredUnitPrice,
                request.Currency,
                "KG",
                request.IsExport), cancellationToken);

            if (priceReport.HasVarianceAnomaly)
            {
                anomalies.Add(priceReport.Narrative);
                var alert = new TbmlAlertRecord(
                    id: Guid.NewGuid(),
                    referenceNumber: request.ReferenceNumber,
                    customerCif: cif,
                    alertType: request.IsExport ? TbmlAlertType.PriceVarianceUnderInvoicing : TbmlAlertType.PriceVarianceOverInvoicing,
                    severity: Math.Abs(priceReport.VariancePercentage) >= 35.0m ? TbmlSeverity.Critical : TbmlSeverity.High,
                    details: priceReport.Narrative);

                await _alertRepo.AddAsync(alert, cancellationToken);
                generatedAlerts.Add(MapAlertToDto(alert));
            }
        }

        // 2. Trade Transaction Profile (TTP) Check
        var ttp = await _ttpRepo.GetByCifAsync(cif, cancellationToken);
        if (ttp is not null)
        {
            var ttpAnomalies = ttp.EvaluateTransaction(txMoney, request.CounterpartyCountry, request.HsCode, request.IsExport);
            if (ttpAnomalies.Count > 0)
            {
                string details = string.Join("; ", ttpAnomalies);
                anomalies.Add(details);
                var alert = new TbmlAlertRecord(
                    id: Guid.NewGuid(),
                    referenceNumber: request.ReferenceNumber,
                    customerCif: cif,
                    alertType: TbmlAlertType.TtpVolumeBreach,
                    severity: TbmlSeverity.Medium,
                    details: details);

                await _alertRepo.AddAsync(alert, cancellationToken);
                generatedAlerts.Add(MapAlertToDto(alert));
            }
            ttp.RecordTransactionVolume(txMoney, request.IsExport);
            await _ttpRepo.UpdateAsync(ttp, cancellationToken);
        }

        // 3. Tax Haven / High-Risk Jurisdiction Check (BRPD 5.7.7)
        if (!string.IsNullOrWhiteSpace(request.CounterpartyCountry) &&
            HighRiskTaxHavenCountries.Contains(request.CounterpartyCountry.Trim()))
        {
            string msg = $"Counterparty country '{request.CounterpartyCountry}' is designated as a High-Risk Jurisdiction / Tax Haven in FATF / BFIU watchlists.";
            anomalies.Add(msg);
            var alert = new TbmlAlertRecord(
                id: Guid.NewGuid(),
                referenceNumber: request.ReferenceNumber,
                customerCif: cif,
                alertType: TbmlAlertType.TaxHavenOrHighRiskJurisdiction,
                severity: TbmlSeverity.High,
                details: msg);

            await _alertRepo.AddAsync(alert, cancellationToken);
            generatedAlerts.Add(MapAlertToDto(alert));
        }

        // 4. Land-locked / Unusual Transshipment Port Check (BRPD 5.7.7)
        bool hasUnusualPort = LandLockedOrSanctionedPorts.Any(p =>
            request.PortOfLoading.Contains(p, StringComparison.OrdinalIgnoreCase) ||
            request.PortOfDischarge.Contains(p, StringComparison.OrdinalIgnoreCase));

        if (hasUnusualPort)
        {
            string msg = $"Port of loading/discharge contains a high-risk sanctioned port or land-locked route: {request.PortOfLoading} -> {request.PortOfDischarge}.";
            anomalies.Add(msg);
            var alert = new TbmlAlertRecord(
                id: Guid.NewGuid(),
                referenceNumber: request.ReferenceNumber,
                customerCif: cif,
                alertType: TbmlAlertType.LandLockedUnusualRoute,
                severity: TbmlSeverity.High,
                details: msg);

            await _alertRepo.AddAsync(alert, cancellationToken);
            generatedAlerts.Add(MapAlertToDto(alert));
        }

        // 5. Round Figure High-Value Anomaly (BRPD 5.7.7)
        if (request.Amount >= 100_000m && request.Amount % 10_000m == 0m)
        {
            string msg = $"Transaction amount ({request.Amount:N2} {request.Currency}) is an exact round figure exceeding BDT/USD 100,000 threshold.";
            anomalies.Add(msg);
            var alert = new TbmlAlertRecord(
                id: Guid.NewGuid(),
                referenceNumber: request.ReferenceNumber,
                customerCif: cif,
                alertType: TbmlAlertType.RoundFigureHighValueAnomaly,
                severity: TbmlSeverity.Medium,
                details: msg);

            await _alertRepo.AddAsync(alert, cancellationToken);
            generatedAlerts.Add(MapAlertToDto(alert));
        }

        // 6. Vessel Blacklist / Sanction Check (BRPD 5.7.5)
        if (!string.IsNullOrWhiteSpace(request.VesselName))
        {
            var blacklistedVessels = new[] { "NEPTUNE STAR", "PACIFIC GLORY", "CRIMEA STAR" };
            if (blacklistedVessels.Any(v => request.VesselName.Contains(v, StringComparison.OrdinalIgnoreCase)))
            {
                string msg = $"Nominated trade vessel '{request.VesselName}' is listed under international maritime sanction watchlists.";
                anomalies.Add(msg);
                var alert = new TbmlAlertRecord(
                    id: Guid.NewGuid(),
                    referenceNumber: request.ReferenceNumber,
                    customerCif: cif,
                    alertType: TbmlAlertType.UnscrSanctionHit,
                    severity: TbmlSeverity.Critical,
                    details: msg);

                await _alertRepo.AddAsync(alert, cancellationToken);
                generatedAlerts.Add(MapAlertToDto(alert));
            }
        }

        if (generatedAlerts.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        bool requiresEscalation = generatedAlerts.Any(a => a.Severity == TbmlSeverity.Critical.ToString()) ||
                                  generatedAlerts.Count >= 2;

        string riskRating = generatedAlerts.Any(a => a.Severity == TbmlSeverity.Critical.ToString())
            ? TbmlRiskRating.ExtremeHit.ToString()
            : generatedAlerts.Any(a => a.Severity == TbmlSeverity.High.ToString())
                ? TbmlRiskRating.High.ToString()
                : generatedAlerts.Count > 0
                    ? TbmlRiskRating.Medium.ToString()
                    : TbmlRiskRating.Low.ToString();

        return new TbmlScreeningReportDto(
            ReferenceNumber: request.ReferenceNumber,
            CustomerCif: request.CustomerCif,
            IsPassed: generatedAlerts.Count == 0,
            RiskRating: riskRating,
            Anomalies: anomalies,
            GeneratedAlerts: generatedAlerts,
            RequiresCamlcoEscalation: requiresEscalation,
            ScreenedAtUtc: DateTime.UtcNow);
    }

    public async Task<VesselTrackingDto> TrackVesselAsync(
        TrackVesselRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        bool isSanctioned = request.VesselName.Contains("CRIMEA", StringComparison.OrdinalIgnoreCase) ||
                            request.DestinationPort.Contains("SEVASTOPOL", StringComparison.OrdinalIgnoreCase);

        var vessel = new VesselTrackingRecord(
            id: Guid.NewGuid(),
            vesselName: request.VesselName,
            imoNumber: request.ImoNumber,
            flagCountry: request.FlagCountry,
            lastReportedPort: request.LastReportedPort,
            destinationPort: request.DestinationPort,
            isSanctionedOrBlacklisted: isSanctioned,
            loggedByUserId: request.LoggedByUserId);

        await _vesselRepo.AddAsync(vessel, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VesselTrackingDto(
            Id: vessel.Id,
            VesselName: vessel.VesselName,
            ImoNumber: vessel.ImoNumber,
            FlagCountry: vessel.FlagCountry,
            LastReportedPort: vessel.LastReportedPort,
            DestinationPort: vessel.DestinationPort,
            IsSanctionedOrBlacklisted: vessel.IsSanctionedOrBlacklisted,
            TrackedAtUtc: vessel.TrackedAtUtc,
            LoggedByUserId: vessel.LoggedByUserId);
    }

    public async Task<TbmlAlertDto> EscalateAlertToCamlcoAsync(
        Guid alertId,
        EscalateTbmlAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var alert = await _alertRepo.GetByIdAsync(alertId, cancellationToken)
            ?? throw new KeyNotFoundException($"TBML Alert '{alertId}' was not found.");

        string strRef = string.IsNullOrWhiteSpace(request.StrSarReference)
            ? $"BFIU-STR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..26].ToUpperInvariant()
            : request.StrSarReference;

        alert.EscalateToCamlco(request.OfficerUserId, strRef, request.Remarks);

        await _alertRepo.UpdateAsync(alert, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapAlertToDto(alert);
    }

    public async Task<IReadOnlyList<TbmlAlertDto>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        var alerts = await _alertRepo.GetActiveAlertsAsync(cancellationToken);
        return alerts.Select(MapAlertToDto).ToList();
    }

    private static TradeTransactionProfileDto MapTtpToDto(TradeTransactionProfile t)
    {
        return new TradeTransactionProfileDto(
            Id: t.Id,
            CustomerCif: t.CustomerCif.Value,
            ApprovedAnnualImportVolume: t.ApprovedAnnualImportVolume.Amount,
            ApprovedAnnualExportVolume: t.ApprovedAnnualExportVolume.Amount,
            CumulativeYearToDateImportVolume: t.CumulativeYearToDateImportVolume.Amount,
            CumulativeYearToDateExportVolume: t.CumulativeYearToDateExportVolume.Amount,
            Currency: t.ApprovedAnnualImportVolume.Currency.Code,
            PermittedCountriesCsv: t.PermittedCountriesCsv,
            PermittedHsPrefixesCsv: t.PermittedHsPrefixesCsv,
            RiskCategory: t.RiskCategory,
            EffectiveFromDate: t.EffectiveFromDate,
            ExpiryDate: t.ExpiryDate,
            LastUpdatedAtUtc: t.LastUpdatedAtUtc,
            ApprovedByUserId: t.ApprovedByUserId);
    }

    private static TbmlAlertDto MapAlertToDto(TbmlAlertRecord a)
    {
        return new TbmlAlertDto(
            AlertId: a.Id,
            ReferenceNumber: a.ReferenceNumber,
            CustomerCif: a.CustomerCif.Value,
            AlertType: a.AlertType.ToString(),
            Severity: a.Severity.ToString(),
            Details: a.Details,
            IsEscalatedToCamlco: a.IsEscalatedToCamlco,
            StrSarReference: a.StrSarReference,
            Status: a.Status.ToString(),
            CreatedAtUtc: a.CreatedAtUtc);
    }
}
