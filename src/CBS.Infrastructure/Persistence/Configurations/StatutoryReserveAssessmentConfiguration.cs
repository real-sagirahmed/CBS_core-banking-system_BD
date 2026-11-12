// ARCHITECTURAL INTENT: EF Core configuration for StatutoryReserveAssessment aggregate root.
// Maps CRR and SLR compliance calculations per MPD Circular 03/2020 & BRPD 12 (6.3.1-l).

using CBS.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public sealed class StatutoryReserveAssessmentConfiguration : IEntityTypeConfiguration<StatutoryReserveAssessment>
{
    public void Configure(EntityTypeBuilder<StatutoryReserveAssessment> builder)
    {
        builder.ToTable("statutory_reserve_assessments");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.AssessmentDate)
            .HasColumnName("assessment_date")
            .IsRequired();

        builder.Property(s => s.BankingType)
            .HasConversion<string>()
            .HasColumnName("banking_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.TdtlBaseBdt)
            .HasColumnName("tdtl_base_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.RequiredCrrDailyBdt)
            .HasColumnName("required_crr_daily_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.RequiredCrrBiWeeklyBdt)
            .HasColumnName("required_crr_biweekly_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.MaintainedCrrCashBdt)
            .HasColumnName("maintained_crr_cash_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.RequiredSlrBdt)
            .HasColumnName("required_slr_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.MaintainedSlrAssetsBdt)
            .HasColumnName("maintained_slr_assets_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.EvaluatedByUserId)
            .HasColumnName("evaluated_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.EvaluatedAtUtc)
            .HasColumnName("evaluated_at_utc")
            .IsRequired();

        builder.Ignore(s => s.DomainEvents);

        builder.HasIndex(s => s.AssessmentDate).HasDatabaseName("ix_statutory_reserves_date");
    }
}
