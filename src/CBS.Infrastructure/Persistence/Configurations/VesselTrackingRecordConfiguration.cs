using CBS.Domain.TradeFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class VesselTrackingRecordConfiguration : IEntityTypeConfiguration<VesselTrackingRecord>
{
    public void Configure(EntityTypeBuilder<VesselTrackingRecord> builder)
    {
        builder.ToTable("vessel_tracking_records");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("vessel_tracking_id")
            .IsRequired();

        builder.Property(v => v.VesselName)
            .HasColumnName("vessel_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(v => v.ImoNumber)
            .HasColumnName("imo_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(v => v.FlagCountry)
            .HasColumnName("flag_country")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(v => v.LastReportedPort)
            .HasColumnName("last_reported_port")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.DestinationPort)
            .HasColumnName("destination_port")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.IsSanctionedOrBlacklisted)
            .HasColumnName("is_sanctioned_or_blacklisted")
            .IsRequired();

        builder.Property(v => v.TrackedAtUtc)
            .HasColumnName("tracked_at_utc")
            .IsRequired();

        builder.Property(v => v.LoggedByUserId)
            .HasColumnName("logged_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Ignore(v => v.DomainEvents);
    }
}
