using CBS.Domain.Shariah;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations.Shariah;

public class MurabahaFinancingFacilityConfiguration : IEntityTypeConfiguration<MurabahaFinancingFacility>
{
    public void Configure(EntityTypeBuilder<MurabahaFinancingFacility> builder)
    {
        builder.ToTable("murabaha_financing_facilities");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("facility_id")
            .IsRequired();

        builder.Property(m => m.FacilityNumber)
            .HasColumnName("facility_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Cif)
            .HasColumnName("cif")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.PurchaseCost)
            .HasColumnName("purchase_cost")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(m => m.ProfitMarginAmount)
            .HasColumnName("profit_margin_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(m => m.RepaymentTenorMonths)
            .HasColumnName("repayment_tenor_months")
            .IsRequired();

        builder.Property(m => m.MonthlyInstallment)
            .HasColumnName("monthly_installment")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(m => m.OutstandingBalance)
            .HasColumnName("outstanding_balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(m => m.GoodsDescription)
            .HasColumnName("goods_description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(m => m.InspectionProofReference)
            .HasColumnName("inspection_proof_reference")
            .HasMaxLength(150);

        builder.Property(m => m.VendorDisbursementOrderJson)
            .HasColumnName("vendor_disbursement_order_json");

        builder.Property(m => m.DisbursedAtUtc)
            .HasColumnName("disbursed_at_utc");

        builder.Property(m => m.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(m => m.FacilityNumber)
            .IsUnique()
            .HasDatabaseName("ux_murabaha_facility_number");

        builder.HasIndex(m => m.Cif)
            .HasDatabaseName("ix_murabaha_cif");
    }
}
