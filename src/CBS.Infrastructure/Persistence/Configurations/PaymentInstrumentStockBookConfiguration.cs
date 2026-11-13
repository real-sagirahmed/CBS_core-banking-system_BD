// ARCHITECTURAL INTENT: EF Core configuration for PaymentInstrumentStockBook aggregate.
// Enforces branch physical stock tracking and joint custody constraints.

using CBS.Domain.PaymentInstruments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class PaymentInstrumentStockBookConfiguration : IEntityTypeConfiguration<PaymentInstrumentStockBook>
{
    public void Configure(EntityTypeBuilder<PaymentInstrumentStockBook> builder)
    {
        builder.ToTable("payment_instrument_stock_books");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("book_id").HasMaxLength(64);

        builder.Property(s => s.BranchCode).HasColumnName("branch_code").HasMaxLength(32).IsRequired();
        builder.Property(s => s.InstrumentType)
            .HasColumnName("instrument_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(s => s.Prefix).HasColumnName("prefix").HasMaxLength(16).IsRequired();
        builder.Property(s => s.StartLeafNumber).HasColumnName("start_leaf_number").IsRequired();
        builder.Property(s => s.EndLeafNumber).HasColumnName("end_leaf_number").IsRequired();
        builder.Property(s => s.TotalLeaves).HasColumnName("total_leaves").IsRequired();
        builder.Property(s => s.IssuedLeavesCount).HasColumnName("issued_leaves_count").IsRequired();
        builder.Property(s => s.NextAvailableLeafNumber).HasColumnName("next_available_leaf_number").IsRequired();

        builder.Property(s => s.CustodianUserId).HasColumnName("custodian_user_id").HasMaxLength(64).IsRequired();
        builder.Property(s => s.AllocatedByUserId).HasColumnName("allocated_by_user_id").HasMaxLength(64).IsRequired();
        builder.Property(s => s.AllocatedAtUtc).HasColumnName("allocated_at_utc").IsRequired();

        builder.Ignore(s => s.IsExhausted);

        builder.HasIndex(s => s.BranchCode);
        builder.HasIndex(s => s.InstrumentType);
        builder.HasIndex(s => new { s.BranchCode, s.InstrumentType });
    }
}
