using CBS.Domain.GeneralLedger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class BranchOperatingDayConfiguration : IEntityTypeConfiguration<BranchOperatingDay>
{
    public void Configure(EntityTypeBuilder<BranchOperatingDay> builder)
    {
        builder.ToTable("branch_operating_days");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(b => b.CurrentBusinessDate)
            .HasConversion(v => v.ToString("yyyy-MM-dd"), v => DateOnly.Parse(v))
            .HasColumnName("current_business_date")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.DayOpenedAtUtc)
            .HasConversion(
                v => v.HasValue ? v.Value.ToUniversalTime().Ticks : (long?)null,
                v => v.HasValue ? new DateTime(v.Value, DateTimeKind.Utc) : null)
            .HasColumnName("opened_at_ticks");

        builder.Property(b => b.DayClosedAtUtc)
            .HasConversion(
                v => v.HasValue ? v.Value.ToUniversalTime().Ticks : (long?)null,
                v => v.HasValue ? new DateTime(v.Value, DateTimeKind.Utc) : null)
            .HasColumnName("closed_at_ticks");

        builder.Property(b => b.ClosedByUserId)
            .HasColumnName("closed_by_user_id")
            .HasMaxLength(50);
    }
}
