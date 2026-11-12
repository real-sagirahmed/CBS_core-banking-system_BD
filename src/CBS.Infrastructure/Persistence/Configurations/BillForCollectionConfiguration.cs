// ARCHITECTURAL INTENT: EF Core configuration for BillForCollection (OBC/IBC)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Sections 2.2.4 & 2.2.5.

using CBS.Domain.Clearing;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class BillForCollectionConfiguration : IEntityTypeConfiguration<BillForCollection>
{
    public void Configure(EntityTypeBuilder<BillForCollection> builder)
    {
        builder.ToTable("bills_for_collection");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BillNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CollectionType)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.CustomerAccountNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CustomerCif)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DrawerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.DraweeName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.DraweeBankName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.DraweeBranchName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.InstrumentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.BillAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("bill_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.ScheduleNumber)
            .HasMaxLength(50);

        builder.Property(x => x.CollectionCommission)
            .HasConversion(v => v != null ? v.Amount : (decimal?)null, v => v.HasValue ? Money.Bdt(v.Value) : null)
            .HasColumnName("collection_commission")
            .HasPrecision(18, 2);

        builder.Property(x => x.PostageCharges)
            .HasConversion(v => v != null ? v.Amount : (decimal?)null, v => v.HasValue ? Money.Bdt(v.Value) : null)
            .HasColumnName("postage_charges")
            .HasPrecision(18, 2);

        builder.Property(x => x.VatAmount)
            .HasConversion(v => v != null ? v.Amount : (decimal?)null, v => v.HasValue ? Money.Bdt(v.Value) : null)
            .HasColumnName("vat_amount")
            .HasPrecision(18, 2);

        builder.Property(x => x.NetProceeds)
            .HasConversion(v => v != null ? v.Amount : (decimal?)null, v => v.HasValue ? Money.Bdt(v.Value) : null)
            .HasColumnName("net_proceeds")
            .HasPrecision(18, 2);

        builder.Property(x => x.DishonourReason)
            .HasMaxLength(500);

        builder.Property(x => x.LodgedByUserId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.VerifiedByCheckerUserId)
            .HasMaxLength(50);

        builder.HasIndex(x => x.BillNumber).IsUnique();
        builder.HasIndex(x => x.CustomerAccountNumber);
        builder.HasIndex(x => x.CustomerCif);
    }
}
