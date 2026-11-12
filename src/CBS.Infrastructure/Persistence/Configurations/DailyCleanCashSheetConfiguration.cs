using CBS.Domain.Reporting;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class DailyCleanCashSheetConfiguration : IEntityTypeConfiguration<DailyCleanCashSheet>
{
    public void Configure(EntityTypeBuilder<DailyCleanCashSheet> builder)
    {
        builder.ToTable("daily_clean_cash_sheets");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("sheet_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.OperatingDate)
            .HasColumnName("operating_date")
            .IsRequired();

        builder.Property(s => s.OpeningVaultCash)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("opening_vault_cash")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.OpeningTellerCash)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("opening_teller_cash")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.TotalCashReceived)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_cash_received")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.TotalCashPaid)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_cash_paid")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.CalculatedClosingCash)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("calculated_closing_cash")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.PhysicalClosingCash)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("physical_closing_cash")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.CashDifference)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("cash_difference")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.ProofStatus)
            .HasConversion<string>()
            .HasColumnName("proof_status")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(s => s.DiscrepancyNotes)
            .HasColumnName("discrepancy_notes")
            .HasMaxLength(1000);

        builder.Property(s => s.CertifiedByHeadCashierUserId)
            .HasColumnName("certified_by_head_cashier_user_id")
            .HasMaxLength(50);

        builder.Property(s => s.ApprovedByManagerUserId)
            .HasColumnName("approved_by_manager_user_id")
            .HasMaxLength(50);

        builder.Property(s => s.CertifiedAtUtc)
            .HasColumnName("certified_at_utc");

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();
    }
}
