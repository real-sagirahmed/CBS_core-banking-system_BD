// ARCHITECTURAL INTENT: EF Core configuration for FatcaComplianceProfile aggregate root.
// Maps 7 US Indicia indicators, Form W-9/W-8BEN, and recalcitrant status per BRPD Circular 12 Module 8.1.15.

using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public sealed class FatcaComplianceProfileConfiguration : IEntityTypeConfiguration<FatcaComplianceProfile>
{
    public void Configure(EntityTypeBuilder<FatcaComplianceProfile> builder)
    {
        builder.ToTable("crm_fatca_compliance_profiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("profile_id")
            .IsRequired();

        builder.Property(p => p.Cif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("cif_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(p => p.Cif)
            .IsUnique();

        builder.Property(p => p.HasUsCitizenshipOrGreenCard)
            .HasColumnName("has_us_citizenship_or_green_card")
            .IsRequired();

        builder.Property(p => p.HasUsPlaceOfBirth)
            .HasColumnName("has_us_place_of_birth")
            .IsRequired();

        builder.Property(p => p.HasUsAddress)
            .HasColumnName("has_us_address")
            .IsRequired();

        builder.Property(p => p.HasUsPhoneNumber)
            .HasColumnName("has_us_phone_number")
            .IsRequired();

        builder.Property(p => p.HasStandingInstructionsToUsAccount)
            .HasColumnName("has_standing_instructions_to_us_acc")
            .IsRequired();

        builder.Property(p => p.HasPowerOfAttorneyGrantedToUsAddress)
            .HasColumnName("has_poa_granted_to_us_address")
            .IsRequired();

        builder.Property(p => p.HasInCareOfOrHoldMailAddress)
            .HasColumnName("has_in_care_of_or_hold_mail")
            .IsRequired();

        builder.Property(p => p.Classification)
            .HasConversion<string>()
            .HasColumnName("fatca_classification")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(p => p.UsTaxpayerIdentificationNumber)
            .HasColumnName("us_tin")
            .HasMaxLength(20);

        builder.Property(p => p.IsFormW9Submitted)
            .HasColumnName("is_form_w9_submitted")
            .IsRequired();

        builder.Property(p => p.FormW9SubmissionDate)
            .HasColumnName("form_w9_submission_date");

        builder.Property(p => p.IsFormW8BenSubmitted)
            .HasColumnName("is_form_w8ben_submitted")
            .IsRequired();

        builder.Property(p => p.FormW8BenSubmissionDate)
            .HasColumnName("form_w8ben_submission_date");

        builder.Property(p => p.GlobalIntermediaryIdentificationNumber)
            .HasColumnName("giin")
            .HasMaxLength(30);

        builder.Property(p => p.IsSubjectToWithholdingTax)
            .HasColumnName("is_subject_to_withholding_tax")
            .IsRequired();

        builder.Property(p => p.IndiciaDetectedDate)
            .HasColumnName("indicia_detected_date");

        builder.Property(p => p.CureDeadlineDate)
            .HasColumnName("cure_deadline_date");

        builder.Property(p => p.EvaluatedByUserId)
            .HasColumnName("evaluated_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.LastEvaluatedAtUtc)
            .HasColumnName("last_evaluated_at_utc")
            .IsRequired();
    }
}
