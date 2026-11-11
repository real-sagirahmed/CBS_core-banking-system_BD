using CBS.Domain.Lending.Agricultural;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations.Specialized;

public class AgriculturalLoanFacilityConfiguration : IEntityTypeConfiguration<AgriculturalLoanFacility>
{
    public void Configure(EntityTypeBuilder<AgriculturalLoanFacility> builder)
    {
        builder.ToTable("agricultural_loan_facilities");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(a => a.Cif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("cif_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.KrishiCardNumber)
            .HasColumnName("krishi_card_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.FarmerSavingsAccount)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("farmer_savings_account")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(a => a.CropType)
            .HasConversion<string>()
            .HasColumnName("crop_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.LandPorchaNumber)
            .HasColumnName("land_porcha_number")
            .HasMaxLength(100);

        builder.Property(a => a.UnionParishadCertificationReference)
            .HasColumnName("union_parishad_ref")
            .HasMaxLength(100);

        builder.Property(a => a.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(a => a.SanctionLimit)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("sanction_limit_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.OutstandingPrincipal)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("outstanding_principal_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.AccruedSimpleInterest)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("accrued_simple_interest_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.AccruedCharges)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("accrued_charges_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.AnnualInterestRate)
            .HasColumnName("annual_interest_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(a => a.SanctionDate)
            .HasColumnName("sanction_date")
            .IsRequired();

        builder.Property(a => a.HarvestMaturityDate)
            .HasColumnName("harvest_maturity_date")
            .IsRequired();

        builder.Property(a => a.DisbursedDate)
            .HasColumnName("disbursed_date");

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.IsNonAccrual)
            .HasColumnName("is_non_accrual")
            .IsRequired();

        builder.Property(a => a.DisasterDeclarationReference)
            .HasColumnName("disaster_declaration_ref")
            .HasMaxLength(100);

        builder.Property(a => a.RescheduledDate)
            .HasColumnName("rescheduled_date");
    }
}
