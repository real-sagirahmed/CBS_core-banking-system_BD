// ARCHITECTURAL INTENT: EF Core configuration for ContinuousLoanGodownInspection entity
// adhering to Bangladesh Bank BRPD Circular No. 12 (4.1.3-iv).

using CBS.Domain.Lending.Continuous;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ContinuousLoanGodownInspectionConfiguration : IEntityTypeConfiguration<ContinuousLoanGodownInspection>
{
    public void Configure(EntityTypeBuilder<ContinuousLoanGodownInspection> builder)
    {
        builder.ToTable("continuous_loan_godown_inspections");

        builder.HasKey(g => g.InspectionId);
        builder.Property(g => g.InspectionId).HasColumnName("inspection_id").HasMaxLength(32);
        builder.Property(g => g.LoanAccountNumber).HasColumnName("loan_account_number").HasMaxLength(32).IsRequired();
        builder.Property(g => g.InspectionDate).HasColumnName("inspection_date").IsRequired();
        builder.Property(g => g.InspectorUserId).HasColumnName("inspector_user_id").HasMaxLength(64).IsRequired();

        builder.Property(g => g.GodownLocation).HasColumnName("godown_location").HasMaxLength(200).IsRequired();
        builder.Property(g => g.InspectedStockValue).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("inspected_stock_value_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(g => g.StockConditionDescription).HasColumnName("stock_condition_description").HasMaxLength(200);

        builder.Property(g => g.InsurancePolicyNumber).HasColumnName("insurance_policy_number").HasMaxLength(64).IsRequired();
        builder.Property(g => g.InsuranceCompany).HasColumnName("insurance_company").HasMaxLength(100).IsRequired();
        builder.Property(g => g.InsuranceExpiryDate).HasColumnName("insurance_expiry_date").IsRequired();
        builder.Property(g => g.InsuranceSumInsured).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("insurance_sum_insured_bdt").HasPrecision(18, 2).IsRequired();

        builder.Property(g => g.IsInsuranceValid).HasColumnName("is_insurance_valid").IsRequired();
        builder.Property(g => g.IsInsuranceAdequate).HasColumnName("is_insurance_adequate").IsRequired();
        builder.Property(g => g.InsurableCappedStockValue).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("insurable_capped_stock_value_bdt").HasPrecision(18, 2).IsRequired();

        builder.Property(g => g.InspectionResult)
            .HasConversion<string>()
            .HasColumnName("inspection_result")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(g => g.Remarks).HasColumnName("remarks").HasMaxLength(500);

        builder.HasIndex(g => g.LoanAccountNumber);
        builder.HasIndex(g => g.InspectionDate);
    }
}
