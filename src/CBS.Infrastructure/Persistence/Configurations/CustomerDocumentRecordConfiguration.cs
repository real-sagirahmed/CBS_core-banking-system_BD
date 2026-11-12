using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class CustomerDocumentRecordConfiguration : IEntityTypeConfiguration<CustomerDocumentRecord>
{
    public void Configure(EntityTypeBuilder<CustomerDocumentRecord> builder)
    {
        builder.ToTable("customer_document_records");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("document_id")
            .IsRequired();

        builder.Property(d => d.CustomerCif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("customer_cif")
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(d => d.CustomerCif);

        builder.Property(d => d.Category)
            .HasConversion<string>()
            .HasColumnName("document_category")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(d => d.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.IssueDate)
            .HasColumnName("issue_date")
            .IsRequired();

        builder.Property(d => d.ExpiryDate)
            .HasColumnName("expiry_date");

        builder.Property(d => d.IssuingAuthority)
            .HasColumnName("issuing_authority")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(d => d.IsVerified)
            .HasColumnName("is_verified")
            .IsRequired();

        builder.Property(d => d.VerifiedByUserId)
            .HasColumnName("verified_by_user_id")
            .HasMaxLength(50);

        builder.Property(d => d.VerifiedAtUtc)
            .HasColumnName("verified_at_utc");

        builder.Property(d => d.UploadedAtUtc)
            .HasColumnName("uploaded_at_utc")
            .IsRequired();

        builder.Property(d => d.IsMandatoryComplianceDocument)
            .HasColumnName("is_mandatory_compliance_document")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(d => d.GracePeriodDays)
            .HasColumnName("grace_period_days")
            .HasDefaultValue(30)
            .IsRequired();
    }
}
