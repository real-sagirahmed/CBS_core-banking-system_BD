using CBS.Domain.BackValue;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class BackValueTransactionRequestConfiguration : IEntityTypeConfiguration<BackValueTransactionRequest>
{
    public void Configure(EntityTypeBuilder<BackValueTransactionRequest> builder)
    {
        builder.ToTable("back_value_transaction_requests");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("request_id").HasMaxLength(64);

        builder.Property(r => r.TargetType).HasColumnName("target_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.TargetIdentifier).HasColumnName("target_identifier").HasMaxLength(64).IsRequired();
        builder.Property(r => r.BranchCode).HasColumnName("branch_code").HasMaxLength(32).IsRequired();
        builder.Property(r => r.BookingDate).HasColumnName("booking_date").IsRequired();
        builder.Property(r => r.ValueDate).HasColumnName("value_date").IsRequired();
        builder.Property(r => r.DaysVariance).HasColumnName("days_variance").IsRequired();

        builder.OwnsOne(r => r.Amount, a =>
        {
            a.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            a.Property(m => m.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(r => r.TransactionNature).HasColumnName("transaction_nature").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(r => r.RegulatoryReference).HasColumnName("regulatory_reference").HasMaxLength(128).IsRequired();
        builder.Property(r => r.MakerUserId).HasColumnName("maker_user_id").HasMaxLength(64).IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.CheckerUserId).HasColumnName("checker_user_id").HasMaxLength(64);
        builder.Property(r => r.CheckerRemarks).HasColumnName("checker_remarks").HasMaxLength(500);
        builder.Property(r => r.ActionedAtUtc).HasColumnName("actioned_at_utc");
        builder.Property(r => r.ExecutedVoucherNumber).HasColumnName("executed_voucher_number").HasMaxLength(64);
        builder.Property(r => r.RetroactiveInterestCalculated).HasColumnName("retroactive_interest_calculated").HasPrecision(18, 2);
        builder.Property(r => r.RetroactiveInterestVoucherNumber).HasColumnName("retroactive_interest_voucher_number").HasMaxLength(64);
        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(r => r.BookingDate);
        builder.HasIndex(r => r.ValueDate);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.MakerUserId);
    }
}
