using CBS.Domain.Security;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TransactionAnomalyRecordConfiguration : IEntityTypeConfiguration<TransactionAnomalyRecord>
{
    public void Configure(EntityTypeBuilder<TransactionAnomalyRecord> builder)
    {
        builder.ToTable("transaction_anomalies");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("anomaly_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.AccountNumber)
            .HasColumnName("account_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.CifNumber)
            .HasColumnName("cif_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.TransactionReference)
            .HasColumnName("transaction_reference")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.AnomalyType)
            .HasConversion<string>()
            .HasColumnName("anomaly_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.TransactionAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("transaction_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.RuleDescription)
            .HasColumnName("rule_description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(a => a.DetectedAtUtc)
            .HasColumnName("detected_at_utc")
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(a => a.InvestigatedByUserId)
            .HasColumnName("investigated_by_user_id")
            .HasMaxLength(50);

        builder.Property(a => a.InvestigationNotes)
            .HasColumnName("investigation_notes")
            .HasMaxLength(1000);

        builder.Property(a => a.ResolvedAtUtc)
            .HasColumnName("resolved_at_utc");
    }
}
