using CBS.Domain.Common;

namespace CBS.Domain.TradeFinance;

// ARCHITECTURAL INTENT: Vessel Tracking Audit Trail Aggregate Root
// recording vessel name, IMO number, flag state, and port itineraries against trade shipments
// to detect sanctioned vessels or suspicious shipping patterns per Bangladesh Bank BRPD Circular 12 (Module 5.7.5).
public sealed class VesselTrackingRecord : AggregateRoot<Guid>
{
    public string VesselName { get; private set; } = null!;
    public string ImoNumber { get; private set; } = null!;
    public string FlagCountry { get; private set; } = null!;
    public string LastReportedPort { get; private set; } = null!;
    public string DestinationPort { get; private set; } = null!;
    public bool IsSanctionedOrBlacklisted { get; private set; }
    public DateTime TrackedAtUtc { get; private set; }
    public string LoggedByUserId { get; private set; } = null!;

    #pragma warning disable CS8618
    private VesselTrackingRecord() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public VesselTrackingRecord(
        Guid id,
        string vesselName,
        string imoNumber,
        string flagCountry,
        string lastReportedPort,
        string destinationPort,
        bool isSanctionedOrBlacklisted,
        string loggedByUserId)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(vesselName))
            throw new ArgumentException("Vessel name is required.", nameof(vesselName));

        if (string.IsNullOrWhiteSpace(imoNumber))
            throw new ArgumentException("IMO number is required.", nameof(imoNumber));

        VesselName = vesselName.Trim().ToUpperInvariant();
        ImoNumber = imoNumber.Trim();
        FlagCountry = flagCountry.Trim().ToUpperInvariant();
        LastReportedPort = lastReportedPort.Trim().ToUpperInvariant();
        DestinationPort = destinationPort.Trim().ToUpperInvariant();
        IsSanctionedOrBlacklisted = isSanctionedOrBlacklisted;
        TrackedAtUtc = DateTime.UtcNow;
        LoggedByUserId = loggedByUserId.Trim();
    }
}
