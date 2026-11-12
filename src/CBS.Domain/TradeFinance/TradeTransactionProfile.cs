using CBS.Domain.Common;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.TradeFinance;

// ARCHITECTURAL INTENT: Customer Trade Transaction Profile (TTP) Aggregate Root
// tracking annual approved import/export turnover, permitted trading partner countries,
// and permitted commodities as per Guidelines for Prevention of Trade Based Money Laundering (TBML)
// and Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 5.7.1 & 5.7.2.
public sealed class TradeTransactionProfile : AggregateRoot<Guid>
{
    public CifNumber CustomerCif { get; private set; } = null!;
    public Money ApprovedAnnualImportVolume { get; private set; } = null!;
    public Money ApprovedAnnualExportVolume { get; private set; } = null!;
    public Money CumulativeYearToDateImportVolume { get; private set; } = null!;
    public Money CumulativeYearToDateExportVolume { get; private set; } = null!;
    public string PermittedCountriesCsv { get; private set; } = null!;
    public string PermittedHsPrefixesCsv { get; private set; } = null!;
    public string RiskCategory { get; private set; } = null!;
    public DateOnly EffectiveFromDate { get; private set; }
    public DateOnly ExpiryDate { get; private set; }
    public DateTime LastUpdatedAtUtc { get; private set; }
    public string ApprovedByUserId { get; private set; } = null!;

    #pragma warning disable CS8618
    private TradeTransactionProfile() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public TradeTransactionProfile(
        Guid id,
        CifNumber customerCif,
        Money approvedAnnualImportVolume,
        Money approvedAnnualExportVolume,
        string permittedCountriesCsv,
        string permittedHsPrefixesCsv,
        string riskCategory,
        DateOnly effectiveFromDate,
        DateOnly expiryDate,
        string approvedByUserId)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(customerCif, nameof(customerCif));
        ArgumentNullException.ThrowIfNull(approvedAnnualImportVolume, nameof(approvedAnnualImportVolume));
        ArgumentNullException.ThrowIfNull(approvedAnnualExportVolume, nameof(approvedAnnualExportVolume));

        CustomerCif = customerCif;
        ApprovedAnnualImportVolume = approvedAnnualImportVolume;
        ApprovedAnnualExportVolume = approvedAnnualExportVolume;
        CumulativeYearToDateImportVolume = Money.Zero(approvedAnnualImportVolume.Currency);
        CumulativeYearToDateExportVolume = Money.Zero(approvedAnnualExportVolume.Currency);
        PermittedCountriesCsv = permittedCountriesCsv?.Trim().ToUpperInvariant() ?? string.Empty;
        PermittedHsPrefixesCsv = permittedHsPrefixesCsv?.Trim() ?? string.Empty;
        RiskCategory = string.IsNullOrWhiteSpace(riskCategory) ? "Medium" : riskCategory.Trim();
        EffectiveFromDate = effectiveFromDate;
        ExpiryDate = expiryDate;
        ApprovedByUserId = approvedByUserId.Trim();
        LastUpdatedAtUtc = DateTime.UtcNow;
    }

    public List<string> EvaluateTransaction(Money transactionAmount, string counterpartyCountry, string hsCode, bool isExport)
    {
        var anomalies = new List<string>();

        // 1. Annual Volume Check
        var limit = isExport ? ApprovedAnnualExportVolume : ApprovedAnnualImportVolume;
        var cumulative = isExport ? CumulativeYearToDateExportVolume : CumulativeYearToDateImportVolume;
        var projected = cumulative + transactionAmount;

        if (projected > limit)
        {
            anomalies.Add($"Projected annual {(isExport ? "export" : "import")} volume ({projected.Amount:N2} {projected.Currency}) exceeds customer approved TTP limit ({limit.Amount:N2} {limit.Currency}).");
        }

        // 2. Permitted Trading Partner Country Check
        if (!string.IsNullOrWhiteSpace(PermittedCountriesCsv))
        {
            var countries = PermittedCountriesCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (!countries.Contains(counterpartyCountry.Trim().ToUpperInvariant()))
            {
                anomalies.Add($"Counterparty country '{counterpartyCountry}' is not in customer's approved TTP permitted trading partner countries.");
            }
        }

        // 3. Commodity / HS Code Check
        if (!string.IsNullOrWhiteSpace(PermittedHsPrefixesCsv) && !string.IsNullOrWhiteSpace(hsCode))
        {
            var prefixes = PermittedHsPrefixesCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            string cleanHs = hsCode.Replace(".", "").Trim();
            if (!prefixes.Any(p => cleanHs.StartsWith(p.Replace(".", "").Trim())))
            {
                anomalies.Add($"HS Code '{hsCode}' does not match customer's approved TTP trade goods line.");
            }
        }

        return anomalies;
    }

    public void RecordTransactionVolume(Money amount, bool isExport)
    {
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        if (isExport)
            CumulativeYearToDateExportVolume += amount;
        else
            CumulativeYearToDateImportVolume += amount;

        LastUpdatedAtUtc = DateTime.UtcNow;
    }
}
