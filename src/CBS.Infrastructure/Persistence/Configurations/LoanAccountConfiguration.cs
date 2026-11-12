using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class LoanAccountConfiguration : IEntityTypeConfiguration<LoanAccount>
{
    public void Configure(EntityTypeBuilder<LoanAccount> builder)
    {
        builder.ToTable("loan_accounts");

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

        builder.Property(a => a.FacilityType)
            .HasConversion<string>()
            .HasColumnName("facility_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(a => a.SanctionLimit)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("sanction_limit_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.DrawingPower)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("drawing_power_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.OutstandingPrincipal)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("outstanding_principal_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.RegularAccruedInterest)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("regular_accrued_interest_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.OverdueInterest)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("overdue_interest_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.PenalInterest)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("penal_interest_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.AccruedCharges)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("accrued_charges_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.InterestSuspense)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("interest_suspense_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.InterestRate)
            .HasColumnName("interest_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(a => a.IsCadVetted)
            .HasColumnName("is_cad_vetted")
            .IsRequired();

        builder.Property(a => a.CadVettedByUserId)
            .HasColumnName("cad_vetted_by_user_id")
            .HasMaxLength(50);

        builder.Property(a => a.CadVettedAtUtc)
            .HasColumnName("cad_vetted_at_utc");

        builder.Property(a => a.SanctionApprovedByUserId)
            .HasColumnName("sanction_approved_by_user_id")
            .HasMaxLength(50);

        builder.Property(a => a.SanctionDate)
            .HasColumnName("sanction_date")
            .IsRequired();

        builder.Property(a => a.MaturityDate)
            .HasColumnName("maturity_date")
            .IsRequired();

        builder.Property(a => a.DisbursedDate)
            .HasColumnName("disbursed_date");

        builder.Property(a => a.DisbursedByUserId)
            .HasColumnName("disbursed_by_user_id")
            .HasMaxLength(50);

        builder.Property(a => a.DisbursementBeneficiarySupplier)
            .HasColumnName("disbursement_beneficiary_supplier")
            .HasMaxLength(100);

        builder.Property(a => a.Classification)
            .HasConversion<string>()
            .HasColumnName("classification_grade")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.DaysPastDue)
            .HasColumnName("days_past_due")
            .IsRequired();

        builder.Property(a => a.IsShariahCompliant)
            .HasColumnName("is_shariah_compliant")
            .IsRequired();

        builder.Property(a => a.EligibleCollateralFsv)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("eligible_collateral_fsv_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.RequiredGeneralProvision)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("required_general_provision_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.RequiredSpecificProvision)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("required_specific_provision_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.RescheduleCount)
            .HasColumnName("reschedule_count")
            .IsRequired();

        builder.Property(a => a.IsWrittenOff)
            .HasColumnName("is_written_off")
            .IsRequired();

        builder.Property(a => a.LoanStatus)
            .HasConversion<string>()
            .HasColumnName("loan_status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.HasQualitativeJudgment)
            .HasColumnName("has_qualitative_judgment")
            .IsRequired();

        builder.Property(a => a.QualitativeJudgmentGrade)
            .HasConversion<string>()
            .HasColumnName("qualitative_judgment_grade")
            .HasMaxLength(30);

        builder.Property(a => a.QualitativeJudgmentReason)
            .HasColumnName("qualitative_judgment_reason")
            .HasMaxLength(200);

        builder.Property(a => a.RescheduledFromLoanAccountNumber)
            .HasColumnName("rescheduled_from_account_number")
            .HasMaxLength(20);

        builder.Ignore(a => a.DomainEvents);

        builder.HasIndex(a => a.Cif).HasDatabaseName("ix_loan_accounts_cif");
        builder.HasIndex(a => a.BranchCode).HasDatabaseName("ix_loan_accounts_branch");
        builder.HasIndex(a => a.Classification).HasDatabaseName("ix_loan_accounts_classification");
    }
}
