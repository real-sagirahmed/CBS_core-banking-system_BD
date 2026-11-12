using CBS.Domain.TradeFinance;

namespace CBS.Application.Common.Interfaces;

public interface IVesselTrackingRepository
{
    Task AddAsync(VesselTrackingRecord vessel, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VesselTrackingRecord>> GetByImoOrNameAsync(string query, CancellationToken cancellationToken = default);
}
