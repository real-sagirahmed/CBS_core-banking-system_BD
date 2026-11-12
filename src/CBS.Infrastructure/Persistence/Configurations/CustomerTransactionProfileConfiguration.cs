using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class CustomerTransactionProfileConfiguration : IEntityTypeConfiguration<CustomerTransactionProfile>
{
    public void Configure(EntityTypeBuilder<CustomerTransactionProfile> builder)
    {
        builder.ToTable("customer_transaction_profiles");

        builder.HasKey(tp => tp.Id);

        builder.Property(tp => tp.Id)
            .HasColumnName("cif_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(tp => tp.MonthlyEstimatedTurnover)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("monthly_estimated_turnover")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tp => tp.MaxMonthlyCashDepositAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("max_monthly_cash_deposit_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tp => tp.MaxMonthlyCashDepositCount)
            .HasColumnName("max_monthly_cash_deposit_count")
            .IsRequired();

        builder.Property(tp => tp.MaxMonthlyCashWithdrawalAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("max_monthly_cash_withdrawal_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tp => tp.MaxMonthlyCashWithdrawalCount)
            .HasColumnName("max_monthly_cash_withdrawal_count")
            .IsRequired();

        builder.Property(tp => tp.MaxMonthlyTransferAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("max_monthly_transfer_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tp => tp.MaxMonthlyTransferCount)
            .HasColumnName("max_monthly_transfer_count")
            .IsRequired();

        builder.Property(tp => tp.MaxMonthlyAdcAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("max_monthly_adc_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tp => tp.MaxMonthlyAdcCount)
            .HasColumnName("max_monthly_adc_count")
            .IsRequired();

        builder.Property(tp => tp.CurrentMonthYear)
            .HasColumnName("current_month_year")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(tp => tp.CurrentMonthCashDepositAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("current_month_cash_deposit_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tp => tp.CurrentMonthCashDepositCount)
            .HasColumnName("current_month_cash_deposit_count")
            .IsRequired();

        builder.Property(tp => tp.CurrentMonthCashWithdrawalAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("current_month_cash_withdrawal_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tp => tp.CurrentMonthCashWithdrawalCount)
            .HasColumnName("current_month_cash_withdrawal_count")
            .IsRequired();

        builder.Property(tp => tp.CurrentMonthTransferAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("current_month_transfer_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tp => tp.CurrentMonthTransferCount)
            .HasColumnName("current_month_transfer_count")
            .IsRequired();

        builder.Property(tp => tp.CurrentMonthAdcAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("current_month_adc_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tp => tp.CurrentMonthAdcCount)
            .HasColumnName("current_month_adc_count")
            .IsRequired();

        builder.Property(tp => tp.IsTpBreached)
            .HasColumnName("is_tp_breached")
            .IsRequired();

        builder.Property(tp => tp.MaxVariancePercentage)
            .HasColumnName("max_variance_percentage")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tp => tp.LastBreachReason)
            .HasColumnName("last_breach_reason")
            .HasMaxLength(500);

        builder.Property(tp => tp.LastAssessedAtUtc)
            .HasColumnName("last_assessed_at_utc")
            .IsRequired();

        builder.Property(tp => tp.LastReviewedByUserId)
            .HasColumnName("last_reviewed_by_user_id")
            .HasMaxLength(50);

        builder.Property(tp => tp.LastReviewedAtUtc)
            .HasColumnName("last_reviewed_at_utc");
    }
}
