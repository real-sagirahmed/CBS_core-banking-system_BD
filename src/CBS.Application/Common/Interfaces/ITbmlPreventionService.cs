using CBS.Application.TradeFinance.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface ITbmlPreventionService
{
    Task<TradeTransactionProfileDto> CreateOrUpdateTtpAsync(CreateOrUpdateTtpRequest request, CancellationToken cancellationToken = default);
    Task<TradeTransactionProfileDto?> GetTtpByCifAsync(string customerCif, CancellationToken cancellationToken = default);
    Task<PriceVerificationReportDto> VerifyPriceAsync(VerifyPriceRequest request, CancellationToken cancellationToken = default);
    Task<TbmlScreeningReportDto> ScreenTradeTransactionAsync(ScreenTbmlRequest request, CancellationToken cancellationToken = default);
    Task<VesselTrackingDto> TrackVesselAsync(TrackVesselRequest request, CancellationToken cancellationToken = default);
    Task<TbmlAlertDto> EscalateAlertToCamlcoAsync(Guid alertId, EscalateTbmlAlertRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TbmlAlertDto>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
}
