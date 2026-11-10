using System.Text.Json;
using CBS.Domain.SecurityAudit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class BfiuRegulatoryReportConfiguration : IEntityTypeConfiguration<BfiuRegulatoryReport>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Configure(EntityTypeBuilder<BfiuRegulatoryReport> builder)
    {
        builder.ToTable("bfiu_regulatory_reports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("report_reference")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(r => r.ReportType)
            .HasColumnName("report_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.ReportDate)
            .HasColumnName("report_date")
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(r => r.GeneratedAtUtc)
            .HasColumnName("generated_at_utc")
            .IsRequired();

        builder.Property(r => r.GeneratedByUserId)
            .HasColumnName("generated_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.GoAmlPayload)
            .HasColumnName("goaml_payload");

        builder.Property(r => r.GoAmlFormat)
            .HasColumnName("goaml_format")
            .HasMaxLength(10);

        builder.Property(r => r.CtrItems)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<CtrReportItem>>(v, JsonOptions) ?? new List<CtrReportItem>())
            .HasColumnName("ctr_items_json")
            .IsRequired();

        builder.Property(r => r.StrAlerts)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<StrAlertItem>>(v, JsonOptions) ?? new List<StrAlertItem>())
            .HasColumnName("str_alerts_json")
            .IsRequired();
    }
}
