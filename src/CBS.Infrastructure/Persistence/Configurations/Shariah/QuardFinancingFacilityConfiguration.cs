using CBS.Domain.Shariah;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations.Shariah;

public class QuardFinancingFacilityConfiguration : IEntityTypeConfiguration<QuardFinancingFacility>
{
    public void Configure(EntityTypeBuilder<QuardFinancingFacility> builder)
    {
        builder.ToTable("quard_financing_facilities");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id)
            .HasColumnName("facility_id")
            .IsRequired();

        builder.Property(q => q.FacilityNumber)
            .HasColumnName("facility_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(q => q.Cif)
            .HasColumnName("cif")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(q => q.PrincipalAmount)
            .HasColumnName("principal_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(q => q.ActualAdministrativeFee)
            .HasColumnName("actual_administrative_fee")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(q => q.InterestRate)
            .HasColumnName("interest_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(q => q.ProfitRate)
            .HasColumnName("profit_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(q => q.OutstandingBalance)
            .HasColumnName("outstanding_balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(q => q.TenorMonths)
            .HasColumnName("tenor_months")
            .IsRequired();

        builder.Property(q => q.IsSettled)
            .HasColumnName("is_settled")
            .IsRequired();

        builder.Property(q => q.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(q => q.FacilityNumber)
            .IsUnique()
            .HasDatabaseName("ux_quard_facility_number");

        builder.HasIndex(q => q.Cif)
            .HasDatabaseName("ix_quard_cif");
    }
}
