using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.TradeFinance;

// ARCHITECTURAL INTENT: Trade-Based Money Laundering (TBML) Alert & Level-3 Escalation Record Aggregate Root
// tracking alerts from price variance, tax havens, land-locked routes, round values, and UNSCR sanction hits
// with automated STR/SAR drafting and CAMLCO escalation per Bangladesh Bank BRPD Circular 12 (5.7.3, 5.7.7, 5.7.8).
public sealed class TbmlAlertRecord : AggregateRoot<Guid>
{
    public string ReferenceNumber { get; private set; } = null!;
    public CifNumber CustomerCif { get; private set; } = null!;
    public TbmlAlertType AlertType { get; private set; }
    public TbmlSeverity Severity { get; private set; }
    public string Details { get; private set; } = null!;
    public bool IsEscalatedToCamlco { get; private set; }
    public string? StrSarReference { get; private set; }
    public TbmlAlertStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewRemarks { get; private set; }

    #pragma warning disable CS8618
    private TbmlAlertRecord() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public TbmlAlertRecord(
        Guid id,
        string referenceNumber,
        CifNumber customerCif,
        TbmlAlertType alertType,
        TbmlSeverity severity,
        string details)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(customerCif, nameof(customerCif));

        ReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber) ? "UNREFERENCED" : referenceNumber.Trim().ToUpperInvariant();
        CustomerCif = customerCif;
        AlertType = alertType;
        Severity = severity;
        Details = details.Trim();
        Status = TbmlAlertStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
        IsEscalatedToCamlco = severity == TbmlSeverity.Critical;
        if (IsEscalatedToCamlco)
        {
            StrSarReference = $"BFIU-STR-AUTO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..28].ToUpperInvariant();
        }
    }

    public void EscalateToCamlco(string officerUserId, string strSarRef, string remarks)
    {
        if (string.IsNullOrWhiteSpace(officerUserId))
            throw new ArgumentException("Officer user ID is required.", nameof(officerUserId));

        if (string.IsNullOrWhiteSpace(strSarRef))
            throw new ArgumentException("STR/SAR regulatory reference number is required.", nameof(strSarRef));

        IsEscalatedToCamlco = true;
        Status = TbmlAlertStatus.EscalatedToCamlco;
        StrSarReference = strSarRef.Trim().ToUpperInvariant();
        ReviewedByUserId = officerUserId.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewRemarks = remarks.Trim();
    }

    public void ClearAlert(string reviewerUserId, string clearanceRemarks)
    {
        if (string.IsNullOrWhiteSpace(reviewerUserId))
            throw new ArgumentException("Reviewer user ID is required.", nameof(reviewerUserId));

        if (string.IsNullOrWhiteSpace(clearanceRemarks))
            throw new ArgumentException("Clearance justification remarks are required.", nameof(clearanceRemarks));

        Status = TbmlAlertStatus.ClearedWithJustification;
        ReviewedByUserId = reviewerUserId.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewRemarks = clearanceRemarks.Trim();
    }
}
