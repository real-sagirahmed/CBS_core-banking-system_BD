using CBS.Domain.Adc;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class AdcTransactionConfiguration : IEntityTypeConfiguration<AdcTransaction>
{
    public void Configure(EntityTypeBuilder<AdcTransaction> builder)
    {
        builder.ToTable("adc_transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("transaction_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.ChannelType)
            .HasConversion<string>()
            .HasColumnName("channel_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.TerminalId)
            .HasColumnName("terminal_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.CardNumberMasked)
            .HasColumnName("card_number_masked")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.Stan)
            .HasColumnName("stan")
            .HasMaxLength(6)
            .IsRequired();

        builder.Property(t => t.Rrn)
            .HasColumnName("rrn")
            .HasMaxLength(12)
            .IsRequired();

        builder.Property(t => t.AccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.IsoResponseCode)
            .HasColumnName("iso_response_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.TerminalCashReflected)
            .HasColumnName("terminal_cash_reflected")
            .IsRequired();

        builder.Property(t => t.Narrative)
            .HasColumnName("narrative")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.VoucherNumber)
            .HasColumnName("voucher_number")
            .HasMaxLength(50);

        builder.Property(t => t.CreatedAtUtc)
            .HasConversion(v => v.ToUniversalTime().Ticks, v => new DateTime(v, DateTimeKind.Utc))
            .HasColumnName("created_at_ticks")
            .IsRequired();

        builder.HasIndex(t => new { t.Stan, t.Rrn });
    }
}
