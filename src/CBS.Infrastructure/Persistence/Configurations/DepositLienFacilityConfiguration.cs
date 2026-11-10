using CBS.Domain.Deposits;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class DepositLienFacilityConfiguration : IEntityTypeConfiguration<DepositLienFacility>
{
    public void Configure(EntityTypeBuilder<DepositLienFacility> builder)
    {
        builder.ToTable("deposit_lien_facilities");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("lien_id")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(l => l.DepositAccountId)
            .HasColumnName("deposit_account_id")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(l => l.DepositType)
            .HasColumnName("deposit_type")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(l => l.LoanAccountId)
            .HasColumnName("loan_account_id")
            .HasMaxLength(60)
            .IsRequired();

        builder.OwnsOne(l => l.SanctionedLoanLimit, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("sanctioned_loan_limit_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("sanctioned_loan_limit_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(l => l.EncumberedAmount, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("encumbered_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("encumbered_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(l => l.MaximumLtvPercentage)
            .HasColumnName("maximum_ltv_percentage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(l => l.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.PlacedAtUtc)
            .HasColumnName("placed_at_utc")
            .IsRequired();

        builder.Property(l => l.ReleasedAtUtc)
            .HasColumnName("released_at_utc");

        builder.Property(l => l.PlacedByOfficerId)
            .HasColumnName("placed_by_officer_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.ReleasedByOfficerId)
            .HasColumnName("released_by_officer_id")
            .HasMaxLength(50);

        builder.Property(l => l.ReferenceDocument)
            .HasColumnName("reference_document")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.ReleaseReason)
            .HasColumnName("release_reason")
            .HasMaxLength(255);

        builder.Ignore(l => l.DomainEvents);

        builder.HasIndex(nameof(DepositLienFacility.DepositAccountId));
        builder.HasIndex(nameof(DepositLienFacility.LoanAccountId));
        builder.HasIndex(nameof(DepositLienFacility.Status));
    }
}
