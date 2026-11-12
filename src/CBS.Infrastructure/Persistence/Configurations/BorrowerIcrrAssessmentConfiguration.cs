// ARCHITECTURAL INTENT: EF Core configuration for BorrowerIcrrAssessment aggregate root.
// Maps 60% Quantitative + 40% Qualitative scoring and Board approval invariants per BRPD Circular 03/2019.

using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public sealed class BorrowerIcrrAssessmentConfiguration : IEntityTypeConfiguration<BorrowerIcrrAssessment>
{
    public void Configure(EntityTypeBuilder<BorrowerIcrrAssessment> builder)
    {
        builder.ToTable("crm_borrower_icrr_assessments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("assessment_id")
            .IsRequired();

        builder.Property(a => a.Cif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("cif_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(a => a.Cif);

        builder.Property(a => a.BorrowerName)
            .HasColumnName("borrower_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.IndustrySector)
            .HasColumnName("industry_sector")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.FinancialStatementDate)
            .HasColumnName("financial_statement_date")
            .IsRequired();

        builder.Property(a => a.AssessmentDate)
            .HasColumnName("assessment_date")
            .IsRequired();

        // Quantitative Scores
        builder.Property(a => a.LeverageScore)
            .HasColumnName("leverage_score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(a => a.LiquidityScore)
            .HasColumnName("liquidity_score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(a => a.ProfitabilityScore)
            .HasColumnName("profitability_score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(a => a.CoverageScore)
            .HasColumnName("coverage_score")
            .HasPrecision(5, 2)
            .IsRequired();

        // Qualitative Scores
        builder.Property(a => a.BusinessRiskScore)
            .HasColumnName("business_risk_score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(a => a.ManagementRiskScore)
            .HasColumnName("management_risk_score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(a => a.SecurityRiskScore)
            .HasColumnName("security_risk_score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(a => a.RatingGrade)
            .HasConversion<string>()
            .HasColumnName("icrr_rating_grade")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.IsBoardOfDirectorsApproved)
            .HasColumnName("is_board_approved")
            .IsRequired();

        builder.Property(a => a.BoardResolutionReference)
            .HasColumnName("board_resolution_reference")
            .HasMaxLength(100);

        builder.Property(a => a.BoardResolutionDate)
            .HasColumnName("board_resolution_date");

        builder.Property(a => a.BoardMitigationStrategy)
            .HasColumnName("board_mitigation_strategy")
            .HasMaxLength(1000);

        builder.Property(a => a.EvaluatedByUserId)
            .HasColumnName("evaluated_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.EvaluatedAtUtc)
            .HasColumnName("evaluated_at_utc")
            .IsRequired();
    }
}
