using System.Text.Json;
using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("cif_number")
            .HasMaxLength(20);

        builder.Property(c => c.Type)
            .HasConversion<string>()
            .HasColumnName("customer_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.DateOfBirth)
            .HasColumnName("date_of_birth")
            .IsRequired();

        builder.Property(c => c.Gender)
            .HasColumnName("gender")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.FatherName)
            .HasColumnName("father_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.MotherName)
            .HasColumnName("mother_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.SpouseName)
            .HasColumnName("spouse_name")
            .HasMaxLength(150);

        builder.Property(c => c.NationalId)
            .HasConversion(
                v => v != null ? v.Value : null,
                v => v != null ? new NationalIdentityNumber(v) : null)
            .HasColumnName("national_id")
            .HasMaxLength(20);

        builder.Property(c => c.TaxId)
            .HasConversion(
                v => v != null ? v.TinNumber : null,
                v => v != null ? new TaxIdentificationNumber(v, null, null) : null)
            .HasColumnName("tax_id")
            .HasMaxLength(20);

        builder.Property(c => c.PresentAddress)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Address>(v, (JsonSerializerOptions?)null)!)
            .HasColumnName("present_address_json")
            .IsRequired();

        builder.Property(c => c.PermanentAddress)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Address>(v, (JsonSerializerOptions?)null)!)
            .HasColumnName("permanent_address_json")
            .IsRequired();

        builder.Property(c => c.MobileNumber)
            .HasColumnName("mobile_number")
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(c => c.EmailAddress)
            .HasColumnName("email_address")
            .HasMaxLength(100);

        builder.Property(c => c.SectorCode)
            .HasColumnName("sector_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.LegalGuardianCif)
            .HasConversion(
                v => v != null ? v.Value : null,
                v => v != null ? new CifNumber(v) : null)
            .HasColumnName("legal_guardian_cif")
            .HasMaxLength(20);

        builder.Property(c => c.CorporateGroupId)
            .HasColumnName("corporate_group_id")
            .HasMaxLength(50);

        builder.Property(c => c.RiskLevel)
            .HasConversion<string>()
            .HasColumnName("risk_level")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.PepStatus)
            .HasConversion<string>()
            .HasColumnName("pep_status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.SanctionStatus)
            .HasConversion<string>()
            .HasColumnName("sanction_status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.FatcaStatus)
            .HasConversion<string>()
            .HasColumnName("fatca_status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.IsElectionCommissionVerified)
            .HasColumnName("is_ec_verified")
            .IsRequired();

        builder.Property(c => c.ElectionCommissionVerifiedAtUtc)
            .HasColumnName("ec_verified_at_utc");

        builder.Property(c => c.TransactionProfile)
            .HasConversion(
                v => v != null ? JsonSerializer.Serialize(v, (JsonSerializerOptions?)null) : null,
                v => v != null ? JsonSerializer.Deserialize<TransactionProfile>(v, (JsonSerializerOptions?)null) : null)
            .HasColumnName("transaction_profile_json");

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasColumnName("lifecycle_status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.OnboardingAudit)
            .HasConversion(
                v => v != null ? JsonSerializer.Serialize(v, (JsonSerializerOptions?)null) : null,
                v => v != null ? JsonSerializer.Deserialize<MakerCheckerAudit>(v, (JsonSerializerOptions?)null) : null)
            .HasColumnName("onboarding_audit_json");

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Ignore(c => c.DomainEvents);

        builder.HasIndex(c => c.NationalId).HasDatabaseName("ix_customers_national_id");
        builder.HasIndex(c => c.TaxId).HasDatabaseName("ix_customers_tax_id");
        builder.HasIndex(c => c.MobileNumber).HasDatabaseName("ix_customers_mobile");
    }
}
