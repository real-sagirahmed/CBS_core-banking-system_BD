using CBS.Domain.Accounts;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class AccountLedgerEntryConfiguration : IEntityTypeConfiguration<AccountLedgerEntry>
{
    public void Configure(EntityTypeBuilder<AccountLedgerEntry> builder)
    {
        builder.ToTable("account_ledger_entries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("entry_id")
            .IsRequired();

        builder.Property(e => e.AccountNumber)
            .HasColumnName("account_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(e => new { e.AccountNumber, e.ValueDate });
        builder.HasIndex(e => e.BookingTimeUtc);

        builder.Property(e => e.ValueDate)
            .HasColumnName("value_date")
            .IsRequired();

        builder.Property(e => e.BookingTimeUtc)
            .HasColumnName("booking_time_utc")
            .IsRequired();

        builder.Property(e => e.VoucherNumber)
            .HasColumnName("voucher_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasColumnName("transaction_type")
            .HasMaxLength(10)
            .IsRequired();

        builder.OwnsOne(e => e.Amount, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(e => e.BalanceAfter, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("balance_after")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("balance_after_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(e => e.Narration)
            .HasColumnName("narration")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.ReferenceNumber)
            .HasColumnName("reference_number")
            .HasMaxLength(60)
            .IsRequired();
    }
}
