using CBS.Domain.Reconciliation;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class DisputeClaimConfiguration : IEntityTypeConfiguration<DisputeClaim>
{
    public void Configure(EntityTypeBuilder<DisputeClaim> builder)
    {
        builder.ToTable("dispute_claims");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("claim_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.OriginalTransactionId)
            .HasColumnName("original_transaction_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.OriginalStan)
            .HasColumnName("original_stan")
            .HasMaxLength(6)
            .IsRequired();

        builder.Property(c => c.OriginalRrn)
            .HasColumnName("original_rrn")
            .HasMaxLength(12)
            .IsRequired();

        builder.Property(c => c.AccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(c => c.DisputedAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("disputed_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.Channel)
            .HasConversion<string>()
            .HasColumnName("channel")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.DisputeReason)
            .HasColumnName("dispute_reason")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.RectificationVoucherNumber)
            .HasColumnName("rectification_voucher_number")
            .HasMaxLength(50);

        builder.Property(c => c.ResolvedByOfficerId)
            .HasColumnName("resolved_by_officer_id")
            .HasMaxLength(50);

        builder.Property(c => c.InvestigationNotes)
            .HasColumnName("investigation_notes")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc)
            .HasConversion(v => v.ToUniversalTime().Ticks, v => new DateTime(v, DateTimeKind.Utc))
            .HasColumnName("created_at_ticks")
            .IsRequired();

        builder.Property(c => c.ResolvedAtUtc)
            .HasConversion(
                v => v.HasValue ? v.Value.ToUniversalTime().Ticks : (long?)null,
                v => v.HasValue ? new DateTime(v.Value, DateTimeKind.Utc) : null)
            .HasColumnName("resolved_at_ticks");
    }
}
