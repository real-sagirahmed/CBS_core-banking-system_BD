using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class SpecimenSignatureConfiguration : IEntityTypeConfiguration<SpecimenSignature>
{
    public void Configure(EntityTypeBuilder<SpecimenSignature> builder)
    {
        builder.ToTable("specimen_signatures");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("signature_id")
            .IsRequired();

        builder.Property(s => s.CustomerCif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("customer_cif")
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(s => s.CustomerCif);

        builder.Property(s => s.SignatoryName)
            .HasColumnName("signatory_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(s => s.Designation)
            .HasColumnName("designation")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.NationalIdOrPassport)
            .HasColumnName("national_id_or_passport")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.Type)
            .HasConversion<string>()
            .HasColumnName("signature_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.EncryptedPayloadBase64)
            .HasColumnName("encrypted_payload_base64")
            .IsRequired();

        builder.Property(s => s.IntegrityChecksumSha256)
            .HasColumnName("integrity_checksum_sha256")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasColumnName("signature_status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.WorkstationIp)
            .HasColumnName("workstation_ip")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(s => s.RevokedAtUtc)
            .HasColumnName("revoked_at_utc");

        builder.Property(s => s.RevocationReason)
            .HasColumnName("revocation_reason")
            .HasMaxLength(255);
    }
}
