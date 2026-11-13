// ARCHITECTURAL INTENT: EF Core configuration for ContinuousLoanFacility aggregate
// enforcing schema, precision, indices, and value conversions under Bangladesh Bank BRPD Circular 12 (4.1, 3.9).

using System;
using CBS.Domain.Enums;
using CBS.Domain.Lending.Continuous;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ContinuousLoanFacilityConfiguration : IEntityTypeConfiguration<ContinuousLoanFacility>
{
    public void Configure(EntityTypeBuilder<ContinuousLoanFacility> builder)
    {
        builder.ToTable("continuous_loan_facilities");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("account_number")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.Cif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("cif_number")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(c => c.Category)
            .HasConversion<string>()
            .HasColumnName("category")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.LinkedDepositAccountNumber)
            .HasColumnName("linked_deposit_account_number")
            .HasMaxLength(32);

        builder.Property(c => c.SanctionLimit)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("sanction_limit_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.DrawingPower)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("drawing_power_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.OutstandingPrincipal)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("outstanding_principal_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.RegularAccruedInterest)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("regular_accrued_interest_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.OverdueInterest)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("overdue_interest_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.PenalInterest)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("penal_interest_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.AccruedCharges)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("accrued_charges_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.InterestSuspense)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("interest_suspense_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.RegularInterestRate)
            .HasColumnName("regular_interest_rate")
            .HasPrecision(8, 4)
            .IsRequired();

        builder.Property(c => c.PenalInterestRate)
            .HasColumnName("penal_interest_rate")
            .HasPrecision(8, 4)
            .IsRequired();

        builder.Property(c => c.SanctionDate).HasColumnName("sanction_date").IsRequired();
        builder.Property(c => c.ExpiryDate).HasColumnName("expiry_date").IsRequired();
        builder.Property(c => c.LastRenewalDate).HasColumnName("last_renewal_date");
        builder.Property(c => c.RenewalCount).HasColumnName("renewal_count").IsRequired();

        builder.Property(c => c.RenewalStatus)
            .HasConversion<string>()
            .HasColumnName("renewal_status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.StockMarginPercent)
            .HasColumnName("stock_margin_percent")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(c => c.BookDebtsMarginPercent)
            .HasColumnName("book_debts_margin_percent")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(c => c.CurrentTodAmount)
            .HasConversion(
                v => v != null ? (decimal?)v.Amount : null,
                v => v.HasValue ? Money.Bdt(v.Value) : null)
            .HasColumnName("current_tod_amount_bdt")
            .HasPrecision(18, 2);

        builder.Property(c => c.CurrentTodExpiryDate).HasColumnName("current_tod_expiry_date");
        builder.Property(c => c.CurrentTodPenalRate).HasColumnName("current_tod_penal_rate").HasPrecision(8, 4);
        builder.Property(c => c.CurrentTodApprovedByUserId).HasColumnName("current_tod_approved_by_user_id").HasMaxLength(64);

        builder.Property(c => c.LastStockStatementDate).HasColumnName("last_stock_statement_date");
        builder.Property(c => c.LastInspectionDate).HasColumnName("last_inspection_date");

        builder.Property(c => c.Classification)
            .HasConversion<string>()
            .HasColumnName("classification")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.DaysPastDue).HasColumnName("days_past_due").IsRequired();
        builder.Property(c => c.IsDebitRestricted).HasColumnName("is_debit_restricted").IsRequired();
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(c => c.Cif);
        builder.HasIndex(c => c.BranchCode);
        builder.HasIndex(c => c.RenewalStatus);
        builder.HasIndex(c => c.ExpiryDate);
        builder.HasIndex(c => c.Classification);
    }
}
