// ARCHITECTURAL INTENT: EF Core configuration for TreasuryLimitProfile aggregate root.
// Maps dealer limits, counterparty lines, and Central Bank NOP ceilings per BRPD Circular 12 (6.2, 6.3).

using CBS.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public sealed class TreasuryLimitProfileConfiguration : IEntityTypeConfiguration<TreasuryLimitProfile>
{
    public void Configure(EntityTypeBuilder<TreasuryLimitProfile> builder)
    {
        builder.ToTable("treasury_limit_profiles");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.LimitType)
            .HasConversion<string>()
            .HasColumnName("limit_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.Identifier)
            .HasColumnName("identifier")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.SanctionedLimit)
            .HasColumnName("sanctioned_limit")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.UtilizedAmount)
            .HasColumnName("utilized_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(t => t.ApprovedByUserId)
            .HasColumnName("approved_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.EffectiveDate)
            .HasColumnName("effective_date")
            .IsRequired();

        builder.Property(t => t.ExpiryDate)
            .HasColumnName("expiry_date");

        builder.Property(t => t.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Ignore(t => t.DomainEvents);

        builder.HasIndex(t => new { t.LimitType, t.Identifier }).HasDatabaseName("ix_treasury_limits_type_id");
    }
}
