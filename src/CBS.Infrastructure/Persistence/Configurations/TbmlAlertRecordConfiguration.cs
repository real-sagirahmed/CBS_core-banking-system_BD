using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TbmlAlertRecordConfiguration : IEntityTypeConfiguration<TbmlAlertRecord>
{
    public void Configure(EntityTypeBuilder<TbmlAlertRecord> builder)
    {
        builder.ToTable("tbml_alert_records");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("alert_id")
            .IsRequired();

        builder.Property(a => a.ReferenceNumber)
            .HasColumnName("reference_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.CustomerCif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("customer_cif")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.AlertType)
            .HasConversion<string>()
            .HasColumnName("alert_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Severity)
            .HasConversion<string>()
            .HasColumnName("severity")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Details)
            .HasColumnName("details")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(a => a.IsEscalatedToCamlco)
            .HasColumnName("is_escalated_to_camlco")
            .IsRequired();

        builder.Property(a => a.StrSarReference)
            .HasColumnName("str_sar_reference")
            .HasMaxLength(50);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(a => a.ReviewedByUserId)
            .HasColumnName("reviewed_by_user_id")
            .HasMaxLength(50);

        builder.Property(a => a.ReviewedAtUtc)
            .HasColumnName("reviewed_at_utc");

        builder.Property(a => a.ReviewRemarks)
            .HasColumnName("review_remarks")
            .HasMaxLength(500);

        builder.Ignore(a => a.DomainEvents);
    }
}
