// ARCHITECTURAL INTENT: EF Core configuration for MtmRevaluationBatch aggregate root.
// Maps MTM revaluation records and revaluation reserves per DOS Circular 05/2008 & BRPD 12 (6.3.1).

using CBS.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public sealed class MtmRevaluationBatchConfiguration : IEntityTypeConfiguration<MtmRevaluationBatch>
{
    public void Configure(EntityTypeBuilder<MtmRevaluationBatch> builder)
    {
        builder.ToTable("mtm_revaluation_batches");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.BatchReference)
            .HasColumnName("batch_reference")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.RevaluationDate)
            .HasColumnName("revaluation_date")
            .IsRequired();

        builder.Property(m => m.TotalForwardDealsEvaluated)
            .HasColumnName("total_forward_deals_evaluated")
            .IsRequired();

        builder.Property(m => m.TotalSecuritiesEvaluated)
            .HasColumnName("total_securities_evaluated")
            .IsRequired();

        builder.Property(m => m.TotalUnrealizedGainsBdt)
            .HasColumnName("total_unrealized_gains_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(m => m.TotalUnrealizedLossesBdt)
            .HasColumnName("total_unrealized_losses_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(m => m.ExecutedByUserId)
            .HasColumnName("executed_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.ExecutedAtUtc)
            .HasColumnName("executed_at_utc")
            .IsRequired();

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(m => m.Remarks)
            .HasColumnName("remarks")
            .HasMaxLength(500);

        builder.Ignore(m => m.DomainEvents);

        builder.HasIndex(m => m.BatchReference).HasDatabaseName("ix_mtm_batches_reference");
    }
}
