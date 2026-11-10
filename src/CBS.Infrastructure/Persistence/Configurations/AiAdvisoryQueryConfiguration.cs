using CBS.Domain.AiIntelligence;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace CBS.Infrastructure.Persistence.Configurations;

public class AiAdvisoryQueryConfiguration : IEntityTypeConfiguration<AiAdvisoryQuery>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Configure(EntityTypeBuilder<AiAdvisoryQuery> builder)
    {
        builder.ToTable("ai_advisory_queries");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id)
            .HasColumnName("query_id")
            .HasMaxLength(64)
            .ValueGeneratedNever();

        builder.Property(q => q.OfficerUserId)
            .HasColumnName("officer_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(q => q.Role)
            .HasConversion<string>()
            .HasColumnName("role")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(q => q.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(q => q.Category)
            .HasConversion<string>()
            .HasColumnName("category")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(q => q.SanitizedQueryText)
            .HasColumnName("sanitized_query_text")
            .IsRequired();

        builder.Property(q => q.AdvisoryResponse)
            .HasColumnName("advisory_response")
            .IsRequired();

        builder.Property(q => q.Citations)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<GroundedCitation>>(v, JsonOptions) ?? new List<GroundedCitation>())
            .HasColumnName("citations_json")
            .IsRequired();

        builder.Property(q => q.ConfidenceScore)
            .HasColumnName("confidence_score")
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(q => q.RequiresMakerCheckerReview)
            .HasColumnName("requires_maker_checker_review")
            .IsRequired();

        builder.Property(q => q.CreatedAtUtc)
            .HasConversion(
                v => v.ToUniversalTime().Ticks,
                v => new DateTime(v, DateTimeKind.Utc))
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(q => q.OfficerUserId);
        builder.HasIndex(q => q.CreatedAtUtc);
    }
}
