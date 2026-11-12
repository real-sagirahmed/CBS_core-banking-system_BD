using CBS.Domain.Accounts;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class StatutoryCertificateRecordConfiguration : IEntityTypeConfiguration<StatutoryCertificateRecord>
{
    public void Configure(EntityTypeBuilder<StatutoryCertificateRecord> builder)
    {
        builder.ToTable("statutory_certificate_issuances");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("certificate_id")
            .IsRequired();

        builder.Property(c => c.CertificateNumber)
            .HasColumnName("certificate_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(c => c.CertificateNumber).IsUnique();
        builder.HasIndex(c => c.AccountNumber);

        builder.Property(c => c.AccountNumber)
            .HasColumnName("account_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.CifNumber)
            .HasColumnName("cif_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.Type)
            .HasConversion<string>()
            .HasColumnName("certificate_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.IssuedAtUtc)
            .HasColumnName("issued_at_utc")
            .IsRequired();

        builder.Property(c => c.IssuedByUserId)
            .HasColumnName("issued_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.OwnsOne(c => c.FeeCharged, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("fee_charged_bdt")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("fee_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(c => c.VatCharged, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("vat_charged_bdt")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("vat_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(c => c.DetailsJson)
            .HasColumnName("details_json")
            .IsRequired();
    }
}
