using CBS.Domain.Shariah;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations.Shariah;

public class ShariahTaWidhCharityFundConfiguration : IEntityTypeConfiguration<ShariahTaWidhCharityFund>
{
    public void Configure(EntityTypeBuilder<ShariahTaWidhCharityFund> builder)
    {
        builder.ToTable("shariah_tawidh_charity_funds");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("record_id")
            .IsRequired();

        builder.Property(t => t.FacilityReference)
            .HasColumnName("facility_reference")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.CustomerCif)
            .HasColumnName("customer_cif")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.OverduePenaltyAmount)
            .HasColumnName("overdue_penalty_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.AccruedAtUtc)
            .HasColumnName("accrued_at_utc")
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.ApprovedSocialWelfareCause)
            .HasColumnName("approved_social_welfare_cause")
            .HasMaxLength(250);

        builder.Property(t => t.DisbursedAtUtc)
            .HasColumnName("disbursed_at_utc");

        builder.Property(t => t.DisbursedByOfficerId)
            .HasColumnName("disbursed_by_officer_id")
            .HasMaxLength(50);

        builder.HasIndex(t => t.FacilityReference)
            .HasDatabaseName("ix_tawidh_facility_reference");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("ix_tawidh_status");
    }
}
