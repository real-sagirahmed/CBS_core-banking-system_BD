using CBS.Domain.Lending.Demand;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class DemandLoanFacilityConfiguration : IEntityTypeConfiguration<DemandLoanFacility>
{
    public void Configure(EntityTypeBuilder<DemandLoanFacility> builder)
    {
        builder.ToTable("demand_loan_facilities");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(d => d.Cif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("cif_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.Type)
            .HasConversion<string>()
            .HasColumnName("demand_loan_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.ProductCode)
            .HasColumnName("product_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(d => d.LoanReferenceNumber)
            .HasColumnName("loan_reference_number")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.MisEconomicSectorCode)
            .HasColumnName("mis_economic_sector_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.MisIndustryCode)
            .HasColumnName("mis_industry_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(d => d.SanctionLimit)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("sanction_limit_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.OutstandingPrincipal)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("outstanding_principal_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.RegularAccruedInterest)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("regular_accrued_interest_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.OverdueInterest)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("overdue_interest_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.PenalInterest)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("penal_interest_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.AccruedCharges)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("accrued_charges_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.InterestSuspense)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("interest_suspense_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.AnnualInterestRate)
            .HasColumnName("annual_interest_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(d => d.PenalInterestRate)
            .HasColumnName("penal_interest_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(d => d.SanctionDate)
            .HasColumnName("sanction_date")
            .IsRequired();

        builder.Property(d => d.MaturityDate)
            .HasColumnName("maturity_date")
            .IsRequired();

        builder.Property(d => d.DisbursedDate)
            .HasColumnName("disbursed_date");

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(d => d.RepaymentPriority)
            .HasConversion<string>()
            .HasColumnName("repayment_priority")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(d => d.CapitalizedDate)
            .HasColumnName("capitalized_date");

        builder.Property(d => d.CapitalizationApprovalReference)
            .HasColumnName("capitalization_approval_ref")
            .HasMaxLength(100);

        builder.HasIndex(d => d.LoanReferenceNumber);
        builder.HasIndex(d => d.BranchCode);
        builder.HasIndex(d => d.Status);
    }
}
