using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.TradeFinance;

/// <summary>
/// Export Regulatory (EXP) Form Tracking Aggregate Root conforming to Bangladesh Bank Foreign Exchange Guidelines
/// and BRPD Circular 12, Clauses 5.2.1, 5.2.7, 5.2.10.
/// Invariants:
/// 1. Mandatory 120-day (4 months) regulatory realization deadline from date of shipment (BRPD 5.2.10).
/// 2. If proceeds are not realized within 120 days, bill is marked Overdue and flagged for statutory reporting to Bangladesh Bank.
/// 3. Cannot realize more than the total invoice value.
/// </summary>
public sealed class ExpRegulatoryTracking : AggregateRoot<string>
{
    public string ExpNumber => Id;
    public string ExportLcOrContractNumber { get; private set; } = null!;
    public Guid ExporterCustomerId { get; private set; }
    public string DestinationCountry { get; private set; } = null!;
    public Money InvoiceAmount { get; private set; } = null!;
    public Money RealizedAmount { get; private set; } = null!;
    public DateTime ShipmentDate { get; private set; }
    public DateTime RealizationDeadlineDate { get; private set; }
    public DateTime? FullyRealizedAtUtc { get; private set; }
    public ExpTrackingStatus Status { get; private set; }
    public bool ReportedToBangladeshBank { get; private set; }
    public string MakerUserId { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    private ExpRegulatoryTracking(string expNumber) : base(expNumber) { }

    public static ExpRegulatoryTracking Register(
        string expNumber,
        string exportLcOrContractNumber,
        Guid exporterCustomerId,
        string destinationCountry,
        Money invoiceAmount,
        DateTime shipmentDate,
        string makerUserId)
    {
        if (string.IsNullOrWhiteSpace(expNumber))
            throw new ArgumentException("EXP Number is required.", nameof(expNumber));
        if (string.IsNullOrWhiteSpace(exportLcOrContractNumber))
            throw new ArgumentException("Export LC or Contract Number is required.", nameof(exportLcOrContractNumber));
        if (string.IsNullOrWhiteSpace(destinationCountry))
            throw new ArgumentException("Destination country is required.", nameof(destinationCountry));
        if (string.IsNullOrWhiteSpace(makerUserId))
            throw new ArgumentException("Maker user ID is required.", nameof(makerUserId));

        // BRPD 5.2.10: 120 days (4 months) statutory deadline from shipment date
        var deadline = shipmentDate.Date.AddDays(120);

        return new ExpRegulatoryTracking(expNumber.Trim())
        {
            ExportLcOrContractNumber = exportLcOrContractNumber.Trim(),
            ExporterCustomerId = exporterCustomerId,
            DestinationCountry = destinationCountry.Trim().ToUpperInvariant(),
            InvoiceAmount = invoiceAmount,
            RealizedAmount = Money.Zero(invoiceAmount.Currency),
            ShipmentDate = shipmentDate.Date,
            RealizationDeadlineDate = deadline,
            Status = ExpTrackingStatus.InTransit,
            ReportedToBangladeshBank = false,
            MakerUserId = makerUserId.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Records realization of export proceeds received through Nostro settlement.
    /// </summary>
    public void RecordRealization(Money receivedAmount)
    {
        if (Status == ExpTrackingStatus.Realized)
            throw new InvalidOperationException("Export proceeds for this EXP are already fully realized.");

        if (receivedAmount.Currency != InvoiceAmount.Currency)
            throw new InvalidOperationException("Realization currency must match EXP invoice currency.");

        var newTotal = RealizedAmount.Amount + receivedAmount.Amount;
        if (newTotal > InvoiceAmount.Amount)
            throw new InvalidOperationException($"Realization amount ({newTotal}) cannot exceed total invoice value ({InvoiceAmount.Amount}).");

        RealizedAmount = new Money(newTotal, InvoiceAmount.Currency);

        if (RealizedAmount.Amount >= InvoiceAmount.Amount)
        {
            Status = ExpTrackingStatus.Realized;
            FullyRealizedAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Evaluates whether the EXP is overdue as per statutory 120-day rule (BRPD 5.2.10).
    /// </summary>
    public bool EvaluateOverdueStatus(DateTime evaluationDate)
    {
        if (Status == ExpTrackingStatus.Realized)
            return false;

        if (evaluationDate.Date > RealizationDeadlineDate)
        {
            Status = ExpTrackingStatus.OverdueReportedToBangladeshBank;
            ReportedToBangladeshBank = true;
            return true;
        }

        return false;
    }
}
