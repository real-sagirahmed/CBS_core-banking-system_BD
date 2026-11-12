// ARCHITECTURAL INTENT: EF Core configuration for NostroMirrorReconciliationItem aggregate root.
// Maps Nostro local mirror entries, statement matching, and aging provisions per BRPD 12 (6.4.2).

using CBS.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public sealed class NostroMirrorReconciliationItemConfiguration : IEntityTypeConfiguration<NostroMirrorReconciliationItem>
{
    public void Configure(EntityTypeBuilder<NostroMirrorReconciliationItem> builder)
    {
        builder.ToTable("nostro_mirror_reconciliation_items");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.NostroAccountCode)
            .HasColumnName("nostro_account_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(n => n.ValueDate)
            .HasColumnName("value_date")
            .IsRequired();

        builder.Property(n => n.EntryType)
            .HasColumnName("entry_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(n => n.LocalBookReference)
            .HasColumnName("local_book_reference")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(n => n.LocalBookAmount)
            .HasColumnName("local_book_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(n => n.StatementReference)
            .HasColumnName("statement_reference")
            .HasMaxLength(100);

        builder.Property(n => n.StatementAmount)
            .HasColumnName("statement_amount")
            .HasPrecision(18, 2);

        builder.Property(n => n.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(n => n.MatchRule)
            .HasColumnName("match_rule")
            .HasMaxLength(50);

        builder.Property(n => n.MatchedAtUtc)
            .HasColumnName("matched_at_utc");

        builder.Property(n => n.MatchedByUserId)
            .HasColumnName("matched_by_user_id")
            .HasMaxLength(50);

        builder.Property(n => n.DaysOutstanding)
            .HasColumnName("days_outstanding")
            .IsRequired();

        builder.Property(n => n.RequiredProvisionRate)
            .HasColumnName("required_provision_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(n => n.RequiredProvisionAmount)
            .HasColumnName("required_provision_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(n => n.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Ignore(n => n.DomainEvents);

        builder.HasIndex(n => new { n.NostroAccountCode, n.Status }).HasDatabaseName("ix_nostro_account_status");
        builder.HasIndex(n => n.LocalBookReference).HasDatabaseName("ix_nostro_local_ref");
    }
}
