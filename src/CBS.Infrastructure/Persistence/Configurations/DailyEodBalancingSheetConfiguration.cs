using CBS.Domain.Reporting;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class DailyEodBalancingSheetConfiguration : IEntityTypeConfiguration<DailyEodBalancingSheet>
{
    public void Configure(EntityTypeBuilder<DailyEodBalancingSheet> builder)
    {
        builder.ToTable("daily_eod_balancing_sheets");

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

        builder.Property(s => s.TotalGlDebits)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_gl_debits")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.TotalGlCredits)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_gl_credits")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.TotalVaultCashBalance)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_vault_cash_balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.TotalTellerCashBalance)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_teller_cash_balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.TotalClearingSettlementDebits)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_clearing_settlement_debits")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.TotalClearingSettlementCredits)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_clearing_settlement_credits")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.ClearingSuspenseBalance)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("clearing_suspense_balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.ProofingStatus)
            .HasConversion<string>()
            .HasColumnName("proofing_status")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(s => s.DiscrepancyNotes)
            .HasColumnName("discrepancy_notes")
            .HasMaxLength(1000);

        builder.Property(s => s.ApprovedByManagerUserId)
            .HasColumnName("approved_by_manager_user_id")
            .HasMaxLength(50);

        builder.Property(s => s.ProofedAtUtc)
            .HasColumnName("proofed_at_utc");

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();
    }
}
