using CBS.Domain.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class SystemUserManualTopicConfiguration : IEntityTypeConfiguration<SystemUserManualTopic>
{
    public void Configure(EntityTypeBuilder<SystemUserManualTopic> builder)
    {
        builder.ToTable("system_user_manual_topics");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("topic_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.ModuleId)
            .HasConversion<string>()
            .HasColumnName("module_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.ModuleTitle)
            .HasColumnName("module_title")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.TopicTitle)
            .HasColumnName("topic_title")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(t => t.Summary)
            .HasColumnName("summary")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.StepByStepGuideMarkdown)
            .HasColumnName("step_by_step_guide_markdown")
            .IsRequired();

        builder.Property(t => t.RegulatoryClauses)
            .HasColumnName("regulatory_clauses")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(t => t.InvariantsEnforced)
            .HasColumnName("invariants_enforced")
            .HasMaxLength(250)
            .IsRequired();

        builder.HasIndex(t => t.ModuleId)
            .HasDatabaseName("ix_user_manuals_module");
    }
}
