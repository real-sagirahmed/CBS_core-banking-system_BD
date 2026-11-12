// ARCHITECTURAL INTENT: EF Core configuration for CustomerRiskAssessment aggregate root.
// Maps customer risk grading columns and index per BRPD Circular 12 Module 8.1.13.

using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public sealed class CustomerRiskAssessmentConfiguration : IEntityTypeConfiguration<CustomerRiskAssessment>
{
    public void Configure(EntityTypeBuilder<CustomerRiskAssessment> builder)
    {
        builder.ToTable("crm_customer_risk_assessments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("assessment_id")
            .IsRequired();

        builder.Property(a => a.Cif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("cif_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(a => a.Cif)
            .IsUnique();

        builder.Property(a => a.Channel)
            .HasConversion<string>()
            .HasColumnName("onboarding_channel")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.IsBorderOrHighRiskArea)
            .HasColumnName("is_border_or_high_risk_area")
            .IsRequired();

        builder.Property(a => a.IsPoliticallyExposedPerson)
            .HasColumnName("is_pep")
            .IsRequired();

        builder.Property(a => a.IsInfluentialPersonOrAssociate)
            .HasColumnName("is_influential_person_or_associate")
            .IsRequired();

        builder.Property(a => a.ProductRiskScore)
            .HasColumnName("product_risk_score")
            .IsRequired();

        builder.Property(a => a.OccupationRiskScore)
            .HasColumnName("occupation_risk_score")
            .IsRequired();

        builder.Property(a => a.CompositeRiskScore)
            .HasColumnName("composite_risk_score")
            .IsRequired();

        builder.Property(a => a.RiskGrade)
            .HasConversion<string>()
            .HasColumnName("risk_grade")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.IsEnhancedDueDiligenceRequired)
            .HasColumnName("is_edd_required")
            .IsRequired();

        builder.Property(a => a.AssessmentDate)
            .HasColumnName("assessment_date")
            .IsRequired();

        builder.Property(a => a.NextReviewDate)
            .HasColumnName("next_review_date")
            .IsRequired();

        builder.Property(a => a.EvaluatedByUserId)
            .HasColumnName("evaluated_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.SeniorManagementApprovedByUserId)
            .HasColumnName("senior_mgmt_approved_by_user_id")
            .HasMaxLength(50);

        builder.Property(a => a.SeniorManagementApprovedAtUtc)
            .HasColumnName("senior_mgmt_approved_at_utc");

        builder.Property(a => a.RiskAssessmentNotes)
            .HasColumnName("risk_assessment_notes")
            .HasMaxLength(1000);

        builder.Property(a => a.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();
    }
}
