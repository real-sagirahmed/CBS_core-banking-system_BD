using CBS.Domain.AiIntelligence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TbmlSemanticAssessmentConfiguration : IEntityTypeConfiguration<TbmlSemanticAssessment>
{
    public void Configure(EntityTypeBuilder<TbmlSemanticAssessment> builder)
    {
        builder.ToTable("tbml_semantic_assessments");

        builder.HasKey(a => a.AssessmentId);

        builder.Property(a => a.AssessmentId)
            .HasColumnName("assessment_id")
            .HasMaxLength(64)
            .ValueGeneratedNever();

        builder.Property(a => a.LcReference)
            .HasColumnName("lc_reference")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(a => a.GoodsDescription)
            .HasColumnName("goods_description")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(a => a.DeclaredUnitPrice)
            .HasColumnName("declared_unit_price")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(a => a.BenchmarkUnitPrice)
            .HasColumnName("benchmark_unit_price")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(a => a.UnitPriceVariancePercentage)
            .HasColumnName("unit_price_variance_percentage")
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(a => a.DualUseRiskIdentified)
            .HasColumnName("dual_use_risk_identified")
            .IsRequired();

        builder.Property(a => a.SanctionedRouteRiskIdentified)
            .HasColumnName("sanctioned_route_risk_identified")
            .IsRequired();

        builder.Property(a => a.Recommendation)
            .HasConversion<string>()
            .HasColumnName("recommendation")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.AnomalyRiskScore)
            .HasColumnName("anomaly_risk_score")
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(a => a.Rationale)
            .HasColumnName("rationale")
            .IsRequired();

        builder.Property(a => a.RequiresMakerCheckerReview)
            .HasColumnName("requires_maker_checker_review")
            .IsRequired();

        builder.Property(a => a.EvaluatedAtUtc)
            .HasConversion(
                v => v.ToUniversalTime().Ticks,
                v => new DateTime(v, DateTimeKind.Utc))
            .HasColumnName("evaluated_at_utc")
            .IsRequired();

        builder.HasIndex(a => a.LcReference);
        builder.HasIndex(a => a.EvaluatedAtUtc);
    }
}
