using CBS.Domain.Deposits;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class DpsInstallmentScheduleConfiguration : IEntityTypeConfiguration<DpsInstallmentSchedule>
{
    public void Configure(EntityTypeBuilder<DpsInstallmentSchedule> builder)
    {
        builder.ToTable("dps_installment_schedules");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("schedule_id")
            .HasMaxLength(70)
            .IsRequired();

        builder.Property(s => s.DpsAccountId)
            .HasColumnName("dps_account_id")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(s => s.InstallmentNumber)
            .HasColumnName("installment_number")
            .IsRequired();

        builder.Property(s => s.DueDate)
            .HasColumnName("due_date")
            .IsRequired();

        builder.Property(s => s.PaidDate)
            .HasColumnName("paid_date");

        builder.OwnsOne(s => s.DueAmount, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("due_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("due_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(s => s.LateFee, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("late_fee_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("late_fee_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.PaymentVoucherNumber)
            .HasColumnName("payment_voucher_number")
            .HasMaxLength(60);

        builder.HasIndex(nameof(DpsInstallmentSchedule.DpsAccountId), nameof(DpsInstallmentSchedule.InstallmentNumber)).IsUnique();
        builder.HasIndex(nameof(DpsInstallmentSchedule.DueDate));
    }
}
