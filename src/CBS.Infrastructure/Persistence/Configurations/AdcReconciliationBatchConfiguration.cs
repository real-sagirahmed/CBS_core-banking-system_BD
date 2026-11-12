using System;
using System.Collections.Generic;
using System.Text.Json;
using CBS.Domain.Adc;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class AdcReconciliationBatchConfiguration : IEntityTypeConfiguration<AdcReconciliationBatch>
{
    public void Configure(EntityTypeBuilder<AdcReconciliationBatch> builder)
    {
        builder.ToTable("adc_reconciliation_batches");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("batch_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.Network)
            .HasConversion<string>()
            .HasColumnName("network")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.ReconciliationDate)
            .HasColumnName("reconciliation_date")
            .IsRequired();

        builder.Property(b => b.SettlementGlAccount)
            .HasColumnName("settlement_gl_account")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.SuspenseGlAccount)
            .HasColumnName("suspense_gl_account")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.TotalRecords)
            .HasColumnName("total_records")
            .IsRequired();

        builder.Property(b => b.TotalMatched)
            .HasColumnName("total_matched")
            .IsRequired();

        builder.Property(b => b.TotalMismatched)
            .HasColumnName("total_mismatched")
            .IsRequired();

        builder.Property(b => b.TotalDiscrepancyAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_discrepancy_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(b => b.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(b => b.ClosedAtUtc)
            .HasColumnName("closed_at_utc");

        builder.Property(b => b.Entries)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<AdcReconciliationEntry>>(v, (JsonSerializerOptions?)null) ?? new List<AdcReconciliationEntry>())
            .HasColumnName("entries_json")
            .HasDefaultValueSql("'[]'");

        builder.Property(b => b.Vouchers)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<RectificationVoucherRecord>>(v, (JsonSerializerOptions?)null) ?? new List<RectificationVoucherRecord>())
            .HasColumnName("vouchers_json")
            .HasDefaultValueSql("'[]'");
    }
}
