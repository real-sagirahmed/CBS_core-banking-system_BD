// ARCHITECTURAL INTENT: EF Core configuration for ContinuousLoanStockStatement entity
// adhering to Bangladesh Bank BRPD Circular No. 12 (4.1.4, 4.1.6).

using CBS.Domain.Lending.Continuous;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ContinuousLoanStockStatementConfiguration : IEntityTypeConfiguration<ContinuousLoanStockStatement>
{
    public void Configure(EntityTypeBuilder<ContinuousLoanStockStatement> builder)
    {
        builder.ToTable("continuous_loan_stock_statements");

        builder.HasKey(s => s.StatementId);
        builder.Property(s => s.StatementId).HasColumnName("statement_id").HasMaxLength(32);
        builder.Property(s => s.LoanAccountNumber).HasColumnName("loan_account_number").HasMaxLength(32).IsRequired();
        builder.Property(s => s.StatementDate).HasColumnName("statement_date").IsRequired();
        builder.Property(s => s.AsOfPeriodDate).HasColumnName("as_of_period_date").IsRequired();

        builder.Property(s => s.RawMaterialsValue).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("raw_materials_value_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.WorkInProgressValue).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("work_in_progress_value_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.FinishedGoodsValue).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("finished_goods_value_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.TotalStockValue).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("total_stock_value_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.CreditorsForGoods).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("creditors_for_goods_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.PaidStockValue).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("paid_stock_value_bdt").HasPrecision(18, 2).IsRequired();

        builder.Property(s => s.BookDebtsUnder90Days).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("book_debts_under_90_days_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.BookDebtsOver90Days).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("book_debts_over_90_days_bdt").HasPrecision(18, 2).IsRequired();

        builder.Property(s => s.StockMarginPercent).HasColumnName("stock_margin_percent").HasPrecision(5, 2).IsRequired();
        builder.Property(s => s.BookDebtsMarginPercent).HasColumnName("book_debts_margin_percent").HasPrecision(5, 2).IsRequired();

        builder.Property(s => s.EligibleStockDrawingPower).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("eligible_stock_dp_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.EligibleBookDebtsDrawingPower).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("eligible_book_debts_dp_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.TotalCalculatedDrawingPower).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("total_calculated_dp_bdt").HasPrecision(18, 2).IsRequired();

        builder.Property(s => s.SubmittedByBorrowerRepresentative).HasColumnName("submitted_by_borrower_rep").HasMaxLength(100).IsRequired();
        builder.Property(s => s.VettedByOfficerUserId).HasColumnName("vetted_by_officer_user_id").HasMaxLength(64).IsRequired();
        builder.Property(s => s.VettedAtUtc).HasColumnName("vetted_at_utc").IsRequired();

        builder.HasIndex(s => s.LoanAccountNumber);
        builder.HasIndex(s => s.AsOfPeriodDate);
    }
}
