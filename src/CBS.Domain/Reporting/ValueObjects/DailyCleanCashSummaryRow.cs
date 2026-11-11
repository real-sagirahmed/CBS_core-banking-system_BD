namespace CBS.Domain.Reporting.ValueObjects;

public sealed record DailyCleanCashSummaryRow
{
    public string BranchCode { get; init; }
    public DateOnly ReportDate { get; init; }
    public decimal OpeningVaultCash { get; init; }
    public decimal OpeningTellerCash { get; init; }
    public decimal TotalCashReceipts { get; init; }
    public decimal TotalCashDisbursements { get; init; }
    public decimal ClosingTellerCash { get; init; }
    public decimal ClosingVaultCash { get; init; }
    public decimal TotalClosingCashInHand { get; init; }

    #pragma warning disable CS8618
    public DailyCleanCashSummaryRow() { }
    #pragma warning restore CS8618

    public DailyCleanCashSummaryRow(
        string branchCode,
        DateOnly reportDate,
        decimal openingVaultCash,
        decimal openingTellerCash,
        decimal totalCashReceipts,
        decimal totalCashDisbursements,
        decimal closingTellerCash,
        decimal closingVaultCash)
    {
        BranchCode = branchCode ?? throw new ArgumentNullException(nameof(branchCode));
        ReportDate = reportDate;
        OpeningVaultCash = openingVaultCash;
        OpeningTellerCash = openingTellerCash;
        TotalCashReceipts = totalCashReceipts;
        TotalCashDisbursements = totalCashDisbursements;
        ClosingTellerCash = closingTellerCash;
        ClosingVaultCash = closingVaultCash;
        TotalClosingCashInHand = closingTellerCash + closingVaultCash;
    }
}
