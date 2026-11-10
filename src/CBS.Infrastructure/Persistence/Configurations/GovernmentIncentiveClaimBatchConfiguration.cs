using CBS.Domain.Remittance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class GovernmentIncentiveClaimBatchConfiguration : IEntityTypeConfiguration<GovernmentIncentiveClaimBatch>
{
    public void Configure(EntityTypeBuilder<GovernmentIncentiveClaimBatch> builder)
    {
        builder.ToTable("government_incentive_claim_batches");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("claim_batch_id")
            .IsRequired();

        builder.Property(b => b.BatchNumber)
            .HasColumnName("batch_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(b => b.BatchNumber)
            .IsUnique();

        builder.Property(b => b.ClaimPeriod)
            .HasColumnName("claim_period")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.TotalTransactionCount)
            .HasColumnName("total_transaction_count")
            .IsRequired();

        builder.Property(b => b.TotalClaimedIncentiveBdt)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_claimed_incentive_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(b => b.PreparedByUserId)
            .HasColumnName("prepared_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.BatchedAtUtc)
            .HasColumnName("batched_at_utc")
            .IsRequired();

        builder.Property(b => b.SubmittedAtUtc)
            .HasColumnName("submitted_at_utc");

        builder.Property(b => b.ReimbursedAtUtc)
            .HasColumnName("reimbursed_at_utc");

        builder.Property(b => b.CentralBankReference)
            .HasColumnName("central_bank_reference")
            .HasMaxLength(100);
    }
}
