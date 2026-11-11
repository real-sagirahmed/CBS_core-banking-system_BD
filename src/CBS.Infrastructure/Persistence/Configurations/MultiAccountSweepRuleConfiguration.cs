using System.Text.Json;
using CBS.Domain.Enums;
using CBS.Domain.StandingInstructions;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class MultiAccountSweepRuleConfiguration : IEntityTypeConfiguration<MultiAccountSweepRule>
{
    private record TargetJsonDto(string AccountNumber, decimal Percentage, int Priority, decimal? Cap);

    public void Configure(EntityTypeBuilder<MultiAccountSweepRule> builder)
    {
        builder.ToTable("multi_account_sweep_rules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("rule_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Type)
            .HasConversion<string>()
            .HasColumnName("sweep_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.MasterAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("master_account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(r => r.Targets)
            .HasConversion(
                v => JsonSerializer.Serialize(v.Select(t => new TargetJsonDto(
                    t.TargetAccountNumber.Value,
                    t.AllocationPercentage,
                    t.PriorityOrder,
                    t.TargetBalanceCap != null ? t.TargetBalanceCap.Amount : null)), (JsonSerializerOptions?)null),
                v => (JsonSerializer.Deserialize<List<TargetJsonDto>>(v, (JsonSerializerOptions?)null) ?? new List<TargetJsonDto>())
                    .Select(d => new MultiAccountSweepTarget(
                        AccountNumber.From(d.AccountNumber),
                        d.Percentage,
                        d.Priority,
                        d.Cap.HasValue ? Money.Bdt(d.Cap.Value) : null))
                    .ToList())
            .HasColumnName("targets_json")
            .IsRequired();

        builder.Property(r => r.ThresholdAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("threshold_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.MinimumSweepAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("min_sweep_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.Strategy)
            .HasConversion<string>()
            .HasColumnName("allocation_strategy")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => r.MasterAccountNumber).HasDatabaseName("ix_multi_account_sweep_rules_master");
    }
}
