using CBS.Domain.Deposits;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class DpsAccountConfiguration : IEntityTypeConfiguration<DpsAccount>
{
    public void Configure(EntityTypeBuilder<DpsAccount> builder)
    {
        builder.ToTable("dps_accounts");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("dps_account_id")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(d => d.DpsAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("dps_account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(d => d.LinkedCasaAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("linked_casa_account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(d => d.CustomerCif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("customer_cif")
            .HasMaxLength(30)
            .IsRequired();

        builder.OwnsOne(d => d.MonthlyInstallmentAmount, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("monthly_installment_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("monthly_installment_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(d => d.TenorYears)
            .HasColumnName("tenor_years")
            .IsRequired();

        builder.Property(d => d.ContractedAnnualRate)
            .HasColumnName("contracted_annual_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(d => d.PrematureSavingsPenaltyRate)
            .HasColumnName("premature_savings_penalty_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(d => d.DebitDayOfMonth)
            .HasColumnName("debit_day_of_month")
            .IsRequired();

        builder.Property(d => d.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(d => d.MaturityDate)
            .HasColumnName("maturity_date")
            .IsRequired();

        builder.OwnsOne(d => d.TotalPrincipalPaid, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("total_principal_paid_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("total_principal_paid_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(d => d.TotalInterestAccrued, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("total_interest_accrued_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("total_interest_accrued_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(d => d.PaidInstallmentsCount)
            .HasColumnName("paid_installments_count")
            .IsRequired();

        builder.Property(d => d.ConsecutiveDefaultCount)
            .HasColumnName("consecutive_default_count")
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(d => d.SettlementMode)
            .HasConversion<string>()
            .HasColumnName("settlement_mode")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(d => d.IsLienMarked)
            .HasColumnName("is_lien_marked")
            .IsRequired();

        builder.Property(d => d.ActiveLienFacilityId)
            .HasColumnName("active_lien_facility_id")
            .HasMaxLength(60);

        builder.Property(d => d.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(d => d.ClosedAtUtc)
            .HasColumnName("closed_at_utc");

        builder.HasMany(d => d.Installments)
            .WithOne()
            .HasForeignKey(i => i.DpsAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.Installments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(d => d.DomainEvents);

        builder.HasIndex(nameof(DpsAccount.DpsAccountNumber)).IsUnique();
        builder.HasIndex(nameof(DpsAccount.LinkedCasaAccountNumber));
        builder.HasIndex(nameof(DpsAccount.Status));
    }
}
