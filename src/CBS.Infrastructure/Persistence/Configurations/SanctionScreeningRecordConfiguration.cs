// ARCHITECTURAL INTENT: EF Core configuration for SanctionScreeningRecord aggregate root.
// Maps real-time sanction screening columns, indexes, and dual-control resolution per BRPD Circular 12 Module 8.1.14.

using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public sealed class SanctionScreeningRecordConfiguration : IEntityTypeConfiguration<SanctionScreeningRecord>
{
    public void Configure(EntityTypeBuilder<SanctionScreeningRecord> builder)
    {
        builder.ToTable("crm_sanction_screening_records");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("screening_id")
            .IsRequired();

        builder.Property(r => r.Cif)
            .HasConversion(v => v != null ? v.Value : null, v => v != null ? new CifNumber(v) : null)
            .HasColumnName("cif_number")
            .HasMaxLength(30);

        builder.HasIndex(r => r.Cif);

        builder.Property(r => r.FullName)
            .HasColumnName("screened_full_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(r => r.FullName);

        builder.Property(r => r.DateOfBirth)
            .HasColumnName("date_of_birth");

        builder.Property(r => r.Nationality)
            .HasColumnName("nationality")
            .HasMaxLength(50);

        builder.Property(r => r.WatchlistType)
            .HasConversion<string>()
            .HasColumnName("watchlist_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.MatchedEntityName)
            .HasColumnName("matched_entity_name")
            .HasMaxLength(200);

        builder.Property(r => r.MatchedEntityReference)
            .HasColumnName("matched_entity_reference")
            .HasMaxLength(100);

        builder.Property(r => r.MatchConfidenceScore)
            .HasColumnName("match_confidence_score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasColumnName("screening_status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.IsHardBlocked)
            .HasColumnName("is_hard_blocked")
            .IsRequired();

        builder.Property(r => r.InvestigatedByUserId)
            .HasColumnName("investigated_by_user_id")
            .HasMaxLength(50);

        builder.Property(r => r.InvestigationNotes)
            .HasColumnName("investigation_notes")
            .HasMaxLength(1000);

        builder.Property(r => r.InvestigatedAtUtc)
            .HasColumnName("investigated_at_utc");

        builder.Property(r => r.ApprovedByComplianceHeadUserId)
            .HasColumnName("approved_by_compliance_head_user_id")
            .HasMaxLength(50);

        builder.Property(r => r.ApprovedAtUtc)
            .HasColumnName("approved_at_utc");

        builder.Property(r => r.ScreenedAtUtc)
            .HasColumnName("screened_at_utc")
            .IsRequired();
    }
}
