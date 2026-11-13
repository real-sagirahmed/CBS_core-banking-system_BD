using CBS.Domain.BackValue;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class LinkedTransactionReversalRecordConfiguration : IEntityTypeConfiguration<LinkedTransactionReversalRecord>
{
    public void Configure(EntityTypeBuilder<LinkedTransactionReversalRecord> builder)
    {
        builder.ToTable("linked_transaction_reversal_records");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("reversal_record_id").HasMaxLength(64);

        builder.Property(r => r.OriginalVoucherId).HasColumnName("original_voucher_id").HasMaxLength(64).IsRequired();
        builder.Property(r => r.OriginalVoucherNumber).HasColumnName("original_voucher_number").HasMaxLength(64).IsRequired();
        builder.Property(r => r.ReversalReason).HasColumnName("reversal_reason").HasMaxLength(500).IsRequired();
        builder.Property(r => r.ReversedByUserId).HasColumnName("reversed_by_user_id").HasMaxLength(64).IsRequired();
        builder.Property(r => r.AuthorizerUserId).HasColumnName("authorizer_user_id").HasMaxLength(64).IsRequired();
        builder.Property(r => r.ParentReversalVoucherNumber).HasColumnName("parent_reversal_voucher_number").HasMaxLength(64).IsRequired();
        builder.Property(r => r.LinkedReversalsJson).HasColumnName("linked_reversals_json").HasColumnType("text").IsRequired();

        builder.OwnsOne(r => r.TotalReversedAmount, a =>
        {
            a.Property(m => m.Amount).HasColumnName("total_reversed_amount").HasPrecision(18, 2).IsRequired();
            a.Property(m => m.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(r => r.IsCascadeComplete).HasColumnName("is_cascade_complete").IsRequired();
        builder.Property(r => r.ReversedAtUtc).HasColumnName("reversed_at_utc").IsRequired();

        builder.HasIndex(r => r.OriginalVoucherId);
        builder.HasIndex(r => r.OriginalVoucherNumber);
        builder.HasIndex(r => r.ReversedAtUtc);
    }
}
