using System.Text.Json;
using CBS.Domain.Reconciliation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class SettlementReconciliationConfiguration : IEntityTypeConfiguration<SettlementReconciliationBatch>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<SettlementReconciliationBatch> builder)
    {
        builder.ToTable("settlement_reconciliation_batches");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("batch_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.ReconciliationDate)
            .HasConversion(v => v.ToString("yyyy-MM-dd"), v => DateOnly.Parse(v))
            .HasColumnName("reconciliation_date")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(b => b.Channel)
            .HasConversion<string>()
            .HasColumnName("channel")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.TotalCoreTransactions)
            .HasColumnName("total_core_tx")
            .IsRequired();

        builder.Property(b => b.TotalSwitchTransactions)
            .HasColumnName("total_switch_tx")
            .IsRequired();

        builder.Property(b => b.TotalNetworkTransactions)
            .HasColumnName("total_network_tx")
            .IsRequired();

        builder.Property(b => b.MatchedCount)
            .HasColumnName("matched_count")
            .IsRequired();

        builder.Property(b => b.MismatchedCount)
            .HasColumnName("mismatched_count")
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.UnmatchedItems)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<UnmatchedReconItem>()
                    : JsonSerializer.Deserialize<List<UnmatchedReconItem>>(v, JsonOptions) ?? new List<UnmatchedReconItem>())
            .HasColumnName("unmatched_items_json")
            .IsRequired();

        builder.Property(b => b.ExecutedAtUtc)
            .HasConversion(v => v.ToUniversalTime().Ticks, v => new DateTime(v, DateTimeKind.Utc))
            .HasColumnName("executed_at_ticks")
            .IsRequired();
    }
}
