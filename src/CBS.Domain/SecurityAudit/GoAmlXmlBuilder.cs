using System.Security;
using System.Text;

namespace CBS.Domain.SecurityAudit;

/// <summary>
/// Generates compliant BFIU / UNODC goAML Web Portal XML and JSON payloads
/// conforming to Bangladesh Bank BRPD Circular 12 Module 11 and BFIU Circular 26.
/// </summary>
public static class GoAmlXmlBuilder
{
    public static string BuildXml(BfiuRegulatoryReport report, string reportingEntityId = "BFIU-BANK-001")
    {
        ArgumentNullException.ThrowIfNull(report, nameof(report));

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<report xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
        sb.AppendLine($"  <rentity_id>{Escape(reportingEntityId)}</rentity_id>");
        sb.AppendLine("  <submission_code>E</submission_code>");
        sb.AppendLine($"  <report_code>{Escape(report.ReportType)}</report_code>");
        sb.AppendLine($"  <entity_reference>{Escape(report.ReportReference)}</entity_reference>");
        sb.AppendLine($"  <submission_date>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}</submission_date>");
        sb.AppendLine($"  <currency_code_local>BDT</currency_code_local>");
        sb.AppendLine($"  <branch_code>{Escape(report.BranchCode)}</branch_code>");

        if (report.ReportType == "CTR")
        {
            sb.AppendLine("  <transactions>");
            foreach (var item in report.CtrItems)
            {
                sb.AppendLine("    <transaction>");
                sb.AppendLine($"      <transaction_number>CTR-{Escape(item.CifNumber)}-{item.ReportDate:yyyyMMdd}</transaction_number>");
                sb.AppendLine($"      <date_transaction>{item.ReportDate:yyyy-MM-dd}</date_transaction>");
                sb.AppendLine($"      <amount_local>{item.TotalCashAmount:F2}</amount_local>");
                sb.AppendLine($"      <currency_code>{Escape(item.Currency)}</currency_code>");
                sb.AppendLine($"      <t_type>CASH_AGGREGATE_CTR</t_type>");
                sb.AppendLine($"      <party_cif>{Escape(item.CifNumber)}</party_cif>");
                sb.AppendLine($"      <party_name>{Escape(item.CustomerName)}</party_name>");
                sb.AppendLine($"      <transaction_count>{item.TransactionCount}</transaction_count>");
                sb.AppendLine("    </transaction>");
            }
            sb.AppendLine("  </transactions>");
        }
        else
        {
            sb.AppendLine("  <transactions>");
            foreach (var alert in report.StrAlerts)
            {
                sb.AppendLine("    <transaction>");
                sb.AppendLine($"      <transaction_number>STR-{Escape(alert.CifNumber)}-{alert.DetectedAtUtc:yyyyMMddHHmmss}</transaction_number>");
                sb.AppendLine($"      <date_transaction>{alert.DetectedAtUtc:yyyy-MM-ddTHH:mm:ss}</date_transaction>");
                sb.AppendLine($"      <amount_local>{alert.TransactionAmount:F2}</amount_local>");
                sb.AppendLine($"      <currency_code>{Escape(alert.Currency)}</currency_code>");
                sb.AppendLine($"      <indicator_reason>{alert.Reason}</indicator_reason>");
                sb.AppendLine($"      <party_cif>{Escape(alert.CifNumber)}</party_cif>");
                sb.AppendLine($"      <party_name>{Escape(alert.CustomerName)}</party_name>");
                sb.AppendLine($"      <narrative>{Escape(alert.Narrative)}</narrative>");
                sb.AppendLine("    </transaction>");
            }
            sb.AppendLine("  </transactions>");
        }

        sb.AppendLine("</report>");
        return sb.ToString();
    }

    private static string Escape(string text)
    {
        return SecurityElement.Escape(text ?? string.Empty);
    }
}
