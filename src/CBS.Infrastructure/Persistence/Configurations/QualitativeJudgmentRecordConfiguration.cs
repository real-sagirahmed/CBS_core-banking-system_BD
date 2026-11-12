// ARCHITECTURAL INTENT: EF Core configuration for QualitativeJudgmentRecord aggregate root.
// Maps qualitative judgment overrides, reason codes, dual-control inspector/checker IDs,
// and revocation audits per BRPD Circular 12 (4.1.11-iii, 4.2.27-iii, Rule 5.2).

using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public sealed class QualitativeJudgmentRecordConfiguration : IEntityTypeConfiguration<QualitativeJudgmentRecord>
{
    public void Configure(EntityTypeBuilder<QualitativeJudgmentRecord> builder)
    {
        builder.ToTable("qualitative_judgment_records");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.LoanAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("loan_account_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(q => q.OriginalClassification)
            .HasConversion<string>()
            .HasColumnName("original_classification")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(q => q.OverriddenClassification)
            .HasConversion<string>()
            .HasColumnName("overridden_classification")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(q => q.Reason)
            .HasConversion<string>()
            .HasColumnName("reason")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(q => q.JustificationRemarks)
            .HasColumnName("justification_remarks")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(q => q.InspectorUserId)
            .HasColumnName("inspector_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(q => q.CheckerUserId)
            .HasColumnName("checker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(q => q.EffectiveDate)
            .HasColumnName("effective_date")
            .IsRequired();

        builder.Property(q => q.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(q => q.RevokedAtUtc)
            .HasColumnName("revoked_at_utc");

        builder.Property(q => q.RevocationRemarks)
            .HasColumnName("revocation_remarks")
            .HasMaxLength(500);

        builder.Property(q => q.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Ignore(q => q.DomainEvents);

        builder.HasIndex(q => q.LoanAccountNumber).HasDatabaseName("ix_qualitative_judgments_account");
        builder.HasIndex(q => new { q.LoanAccountNumber, q.IsActive }).HasDatabaseName("ix_qualitative_judgments_account_active");
    }
}
