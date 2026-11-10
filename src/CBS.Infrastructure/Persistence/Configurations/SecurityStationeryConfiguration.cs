using CBS.Domain.BranchCash;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class SecurityStationeryConfiguration : IEntityTypeConfiguration<SecurityStationeryInventory>
{
    public void Configure(EntityTypeBuilder<SecurityStationeryInventory> builder)
    {
        builder.ToTable("security_stationery_inventory");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("stationery_id")
            .IsRequired();

        builder.Property(s => s.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(s => s.InstrumentType)
            .HasConversion<string>()
            .HasColumnName("instrument_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.BookPrefix)
            .HasColumnName("book_prefix")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.StartLeaf)
            .HasColumnName("start_leaf")
            .IsRequired();

        builder.Property(s => s.EndLeaf)
            .HasColumnName("end_leaf")
            .IsRequired();

        builder.Property(s => s.CurrentLeaf)
            .HasColumnName("current_leaf")
            .IsRequired();

        builder.Property(s => s.ManagerUserId)
            .HasColumnName("manager_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.CustodianUserId)
            .HasColumnName("custodian_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .IsRequired();
    }
}
