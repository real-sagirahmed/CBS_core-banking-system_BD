using CBS.Domain.Accounts;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class AccountRestraintConfiguration : IEntityTypeConfiguration<AccountRestraint>
{
    public void Configure(EntityTypeBuilder<AccountRestraint> builder)
    {
        builder.ToTable("account_restraints");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("restraint_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.AccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(r => r.RestraintType)
            .HasConversion<string>()
            .HasColumnName("restraint_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Scope)
            .HasConversion<string>()
            .HasColumnName("scope")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.HoldAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("hold_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.ReasonCategory)
            .HasColumnName("reason_category")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.NarrativeReason)
            .HasColumnName("narrative_reason")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.IssuingAuthority)
            .HasColumnName("issuing_authority")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.ReferenceCaseNo)
            .HasColumnName("reference_case_no")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.PlacedByUserId)
            .HasColumnName("placed_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.AuthorizedByUserId)
            .HasColumnName("authorized_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(r => r.ExpiryDate)
            .HasColumnName("expiry_date");

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.ReleasedByUserId)
            .HasColumnName("released_by_user_id")
            .HasMaxLength(50);

        builder.Property(r => r.ReleaseAuthorizedByUserId)
            .HasColumnName("release_auth_by_user_id")
            .HasMaxLength(50);

        builder.Property(r => r.ReleasedAtUtc)
            .HasColumnName("released_at_utc");

        builder.Property(r => r.ReleaseRemarks)
            .HasColumnName("release_remarks")
            .HasMaxLength(500);

        builder.OwnsOne(r => r.ForeclosedAmount, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("foreclosed_amount_bdt")
                .HasPrecision(18, 2);
            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("foreclosed_currency")
                .HasMaxLength(3);
        });

        builder.Property(r => r.ForeclosedSettlementGl)
            .HasColumnName("foreclosed_settlement_gl")
            .HasMaxLength(50);

        builder.Property(r => r.ForeclosedAtUtc)
            .HasColumnName("foreclosed_at_utc");

        builder.HasIndex(r => r.AccountNumber).HasDatabaseName("ix_account_restraints_account");
        builder.HasIndex(r => r.Status).HasDatabaseName("ix_account_restraints_status");
        builder.HasIndex(r => r.RestraintType).HasDatabaseName("ix_account_restraints_type");
    }
}
