using CBS.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class StatutoryReportArchiveConfiguration : IEntityTypeConfiguration<StatutoryReportArchive>
{
    public void Configure(EntityTypeBuilder<StatutoryReportArchive> builder)
    {
        builder.ToTable("statutory_report_archives");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("archive_id")
            .IsRequired();

        builder.Property(r => r.ReportType)
            .HasConversion<string>()
            .HasColumnName("report_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.ReportingCutoffDate)
            .HasColumnName("reporting_cutoff_date")
            .IsRequired();

        builder.Property(r => r.BranchId)
            .HasColumnName("branch_id")
            .HasMaxLength(50);

        builder.Property(r => r.GenerationTimestamp)
            .HasColumnName("generation_timestamp")
            .IsRequired();

        builder.Property(r => r.GeneratedByUserId)
            .HasColumnName("generated_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.ExportFormat)
            .HasConversion<string>()
            .HasColumnName("export_format")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.ContentHash)
            .HasColumnName("content_hash")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.SerializedPayload)
            .HasColumnName("serialized_payload")
            .IsRequired();

        builder.Property(r => r.RecordCount)
            .HasColumnName("record_count")
            .IsRequired();

        builder.Property(r => r.IsSubmitted)
            .HasColumnName("is_submitted")
            .IsRequired();

        builder.Property(r => r.SubmissionReference)
            .HasColumnName("submission_reference")
            .HasMaxLength(100);

        builder.Property(r => r.SubmissionTimestamp)
            .HasColumnName("submission_timestamp");

        builder.HasIndex(r => new { r.ReportType, r.ReportingCutoffDate })
            .HasDatabaseName("ix_statutory_reports_type_date");

        builder.HasIndex(r => r.ContentHash)
            .HasDatabaseName("ix_statutory_reports_hash");
    }
}
