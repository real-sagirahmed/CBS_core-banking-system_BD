using CBS.Domain.StandingInstructions;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class SweepAccountRuleConfiguration : IEntityTypeConfiguration<SweepAccountRule>
{
    public void Configure(EntityTypeBuilder<SweepAccountRule> builder)
    {
        builder.ToTable("sweep_account_rules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("rule_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.PrimaryAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("primary_account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(r => r.SecondaryAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("secondary_account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.OwnsOne(r => r.UpperThreshold, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("upper_threshold_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("upper_threshold_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(r => r.LowerThreshold, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("lower_threshold_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("lower_threshold_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(r => r.MinimumSweepAmount, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("min_sweep_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("min_sweep_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(r => r.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(nameof(SweepAccountRule.PrimaryAccountNumber));
    }
}
