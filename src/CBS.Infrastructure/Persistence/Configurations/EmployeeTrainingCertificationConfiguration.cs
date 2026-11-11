using CBS.Domain.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class EmployeeTrainingCertificationConfiguration : IEntityTypeConfiguration<EmployeeTrainingCertification>
{
    public void Configure(EntityTypeBuilder<EmployeeTrainingCertification> builder)
    {
        builder.ToTable("employee_training_certifications");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("certification_id")
            .IsRequired();

        builder.Property(c => c.EmployeeId)
            .HasColumnName("employee_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.EmployeeName)
            .HasColumnName("employee_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.UserGroup)
            .HasColumnName("user_group")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.CurriculumType)
            .HasConversion<string>()
            .HasColumnName("curriculum_type")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(c => c.TrainingDate)
            .HasColumnName("training_date")
            .IsRequired();

        builder.Property(c => c.ScorePercentage)
            .HasColumnName("score_percentage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(c => c.PassingScorePercentage)
            .HasColumnName("passing_score_percentage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(c => c.IsCertified)
            .HasColumnName("is_certified")
            .IsRequired();

        builder.Property(c => c.CertificationReference)
            .HasColumnName("certification_reference")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.CertifiedAtUtc)
            .HasColumnName("certified_at_utc")
            .IsRequired();

        builder.Property(c => c.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .IsRequired();

        builder.Property(c => c.CertifiedByOfficerId)
            .HasColumnName("certified_by_officer_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(c => c.EmployeeId)
            .HasDatabaseName("ix_employee_certifications_employee");

        builder.HasIndex(c => c.ExpiresAtUtc)
            .HasDatabaseName("ix_employee_certifications_expiry");
    }
}
