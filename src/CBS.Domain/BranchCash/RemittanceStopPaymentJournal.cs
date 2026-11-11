using CBS.Domain.Common;

namespace CBS.Domain.BranchCash;

// ARCHITECTURAL INTENT: Immutable audit journal tracking Stop Payment and Stop Removal/Revocation actions
// across all local remittance instruments (PO, DD, SDR) conforming to BRPD Circular 12/2024 Modules 2.1.1-ix, 2.1.2-vi, 10.3.6.
public class RemittanceStopPaymentJournal : Entity<Guid>
{
    public string InstrumentType { get; private set; }
    public string InstrumentNumber { get; private set; }
    public string BranchCode { get; private set; }
    public string ActionType { get; private set; } // "Stopped" or "Revoked"
    public string Reason { get; private set; }
    public string AuthorizedByUserId { get; private set; }
    public DateTime ActionDateUtc { get; private set; }
    public string? IndemnityBondReference { get; private set; }

    #pragma warning disable CS8618
    private RemittanceStopPaymentJournal() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public RemittanceStopPaymentJournal(
        Guid id,
        string instrumentType,
        string instrumentNumber,
        string branchCode,
        string actionType,
        string reason,
        string authorizedByUserId,
        DateTime actionDateUtc,
        string? indemnityBondReference = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(instrumentType)) throw new ArgumentException("Instrument type is required.", nameof(instrumentType));
        if (string.IsNullOrWhiteSpace(instrumentNumber)) throw new ArgumentException("Instrument number is required.", nameof(instrumentNumber));
        if (string.IsNullOrWhiteSpace(branchCode)) throw new ArgumentException("Branch code is required.", nameof(branchCode));
        if (string.IsNullOrWhiteSpace(actionType)) throw new ArgumentException("Action type is required.", nameof(actionType));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
        if (string.IsNullOrWhiteSpace(authorizedByUserId)) throw new ArgumentException("Authorized user ID is required.", nameof(authorizedByUserId));

        InstrumentType = instrumentType.Trim();
        InstrumentNumber = instrumentNumber.Trim().ToUpperInvariant();
        BranchCode = branchCode.Trim();
        ActionType = actionType.Trim();
        Reason = reason.Trim();
        AuthorizedByUserId = authorizedByUserId.Trim();
        ActionDateUtc = actionDateUtc;
        IndemnityBondReference = indemnityBondReference?.Trim();
    }
}
