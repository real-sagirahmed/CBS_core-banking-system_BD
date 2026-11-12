using System.Text.Json;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TradeTransactionProfileConfiguration : IEntityTypeConfiguration<TradeTransactionProfile>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public void Configure(EntityTypeBuilder<TradeTransactionProfile> builder)
    {
        builder.ToTable("trade_transaction_profiles");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("ttp_id")
            .IsRequired();

        builder.Property(t => t.CustomerCif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("customer_cif")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.ApprovedAnnualImportVolume)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("approved_annual_import_volume_json")
            .IsRequired();

        builder.Property(t => t.ApprovedAnnualExportVolume)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("approved_annual_export_volume_json")
            .IsRequired();

        builder.Property(t => t.CumulativeYearToDateImportVolume)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("cumulative_ytd_import_volume_json")
            .IsRequired();

        builder.Property(t => t.CumulativeYearToDateExportVolume)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("cumulative_ytd_export_volume_json")
            .IsRequired();

        builder.Property(t => t.PermittedCountriesCsv)
            .HasColumnName("permitted_countries_csv")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.PermittedHsPrefixesCsv)
            .HasColumnName("permitted_hs_prefixes_csv")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.RiskCategory)
            .HasColumnName("risk_category")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.EffectiveFromDate)
            .HasColumnName("effective_from_date")
            .IsRequired();

        builder.Property(t => t.ExpiryDate)
            .HasColumnName("expiry_date")
            .IsRequired();

        builder.Property(t => t.LastUpdatedAtUtc)
            .HasColumnName("last_updated_at_utc")
            .IsRequired();

        builder.Property(t => t.ApprovedByUserId)
            .HasColumnName("approved_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Ignore(t => t.DomainEvents);
    }
}
