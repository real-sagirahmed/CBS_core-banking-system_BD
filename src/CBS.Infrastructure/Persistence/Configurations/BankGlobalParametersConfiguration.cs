using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class BankGlobalParametersConfiguration : IEntityTypeConfiguration<BankGlobalParameters>
{
    public void Configure(EntityTypeBuilder<BankGlobalParameters> builder)
    {
        builder.ToTable("bank_global_parameters");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("bank_code").HasMaxLength(32);

        builder.Property(p => p.BankName).HasColumnName("bank_name").HasMaxLength(128).IsRequired();
        builder.Property(p => p.BaseCurrencyCode).HasColumnName("base_currency_code").HasMaxLength(8).IsRequired();
        builder.Property(p => p.LocalCurrencyCode).HasColumnName("local_currency_code").HasMaxLength(8).IsRequired();
        builder.Property(p => p.ChiefLocalOfficeBranchCode).HasColumnName("chief_local_office_branch_code").HasMaxLength(32).IsRequired();
        builder.Property(p => p.WeeklyHolidaysCsv).HasColumnName("weekly_holidays_csv").HasMaxLength(128).IsRequired();
        builder.Property(p => p.FiscalYearStartMonth).HasColumnName("fiscal_year_start_month").IsRequired();
        builder.Property(p => p.FiscalYearStartDay).HasColumnName("fiscal_year_start_day").IsRequired();
        builder.Property(p => p.RealTimeBranchSettlementEnabled).HasColumnName("real_time_branch_settlement_enabled").IsRequired();
        builder.Property(p => p.AllowAlphaCustomerNumber).HasColumnName("allow_alpha_customer_number").IsRequired();
        builder.Property(p => p.MaxExternalAccountNumberLength).HasColumnName("max_external_account_number_length").IsRequired();
        builder.Property(p => p.DailySpotRateListDetailsJson).HasColumnName("daily_spot_rate_list_details_json").HasColumnType("text");
        builder.Property(p => p.LastUpdatedAtUtc).HasColumnName("last_updated_at_utc").IsRequired();
        builder.Property(p => p.LastUpdatedByUserId).HasColumnName("last_updated_by_user_id").HasMaxLength(64).IsRequired();
    }
}
