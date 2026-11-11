using CBS.Domain.Reconciliation;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ReconciliationAgingItemConfiguration : IEntityTypeConfiguration<ReconciliationAgingItem>
{
    public void Configure(EntityTypeBuilder<ReconciliationAgingItem> builder)
    {
        builder.ToTable("reconciliation_aging_items");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("aging_item_id")
            .IsRequired();

        builder.Property(r => r.ChannelType)
            .HasColumnName("channel_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.TraceOrReferenceNumber)
            .HasColumnName("trace_reference_number")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.OriginatingBranchCode)
            .HasColumnName("originating_branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(r => r.RespondingBranchOrCounterparty)
            .HasColumnName("responding_branch_or_counterparty")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.TransactionAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("transaction_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.TransactionDate)
            .HasColumnName("transaction_date")
            .IsRequired();

        builder.Property(r => r.LastAssessmentDate)
            .HasColumnName("last_assessment_date")
            .IsRequired();

        builder.Property(r => r.AgeInDays)
            .HasColumnName("age_in_days")
            .IsRequired();

        builder.Property(r => r.Bucket)
            .HasConversion<string>()
            .HasColumnName("aging_bucket")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.RequiredProvisionPercentage)
            .HasColumnName("required_provision_percentage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(r => r.RequiredProvisionAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("required_provision_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.IsResolved)
            .HasColumnName("is_resolved")
            .IsRequired();

        builder.Property(r => r.ResolvedDate)
            .HasColumnName("resolved_date");

        builder.Property(r => r.ResolutionReference)
            .HasColumnName("resolution_ref")
            .HasMaxLength(100);

        builder.HasIndex(r => r.TraceOrReferenceNumber);
        builder.HasIndex(r => r.ChannelType);
        builder.HasIndex(r => r.Bucket);
        builder.HasIndex(r => r.IsResolved);
    }
}
