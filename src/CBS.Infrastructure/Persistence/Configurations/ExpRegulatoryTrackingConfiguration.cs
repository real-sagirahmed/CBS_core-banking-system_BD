using System.Text.Json;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ExpRegulatoryTrackingConfiguration : IEntityTypeConfiguration<ExpRegulatoryTracking>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Configure(EntityTypeBuilder<ExpRegulatoryTracking> builder)
    {
        builder.ToTable("trade_exp_regulatory_trackings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("exp_number")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.ExportLcOrContractNumber)
            .HasColumnName("export_lc_or_contract_number")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.ExporterCustomerId)
            .HasColumnName("exporter_customer_id")
            .IsRequired();

        builder.Property(e => e.DestinationCountry)
            .HasColumnName("destination_country")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.InvoiceAmount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("invoice_amount_json")
            .IsRequired();

        builder.Property(e => e.RealizedAmount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("realized_amount_json")
            .IsRequired();

        builder.Property(e => e.ShipmentDate)
            .HasColumnName("shipment_date")
            .IsRequired();

        builder.Property(e => e.RealizationDeadlineDate)
            .HasColumnName("realization_deadline_date")
            .IsRequired();

        builder.Property(e => e.FullyRealizedAtUtc)
            .HasColumnName("fully_realized_at_utc");

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ReportedToBangladeshBank)
            .HasColumnName("reported_to_bangladesh_bank")
            .IsRequired();

        builder.Property(e => e.MakerUserId)
            .HasColumnName("maker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Ignore(e => e.DomainEvents);

        builder.HasIndex(e => e.BranchCode).HasDatabaseName("ix_trade_exp_branch");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_trade_exp_status");
        builder.HasIndex(e => e.RealizationDeadlineDate).HasDatabaseName("ix_trade_exp_deadline");
    }
}
