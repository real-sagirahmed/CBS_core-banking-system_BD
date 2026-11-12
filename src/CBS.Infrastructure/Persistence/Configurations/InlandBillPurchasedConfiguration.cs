// ARCHITECTURAL INTENT: EF Core configuration for InlandBillPurchased (IBP)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 2 & 4.

using CBS.Domain.Clearing;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class InlandBillPurchasedConfiguration : IEntityTypeConfiguration<InlandBillPurchased>
{
    public void Configure(EntityTypeBuilder<InlandBillPurchased> builder)
    {
        builder.ToTable("inland_bills_purchased");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IbpReferenceNumber)
            .HasMaxLength(50)
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

        builder.Property(x => x.DraweeBankName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.InstrumentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.FaceValue)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("face_value")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.MarginRatePercent)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.MarginRetained)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("margin_retained")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.DiscountRateAnnualPercent)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.DiscountInterest)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("discount_interest")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.DocumentationFee)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("documentation_fee")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.VatAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("vat_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.DisbursedAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("disbursed_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.ForcedLoanAccountNumber)
            .HasMaxLength(50);

        builder.Property(x => x.CrystallizedOverdueAmount)
            .HasConversion(v => v != null ? v.Amount : (decimal?)null, v => v.HasValue ? Money.Bdt(v.Value) : null)
            .HasColumnName("crystallized_overdue_amount")
            .HasPrecision(18, 2);

        builder.Property(x => x.PenaltyInterestRatePercent)
            .HasPrecision(5, 2);

        builder.Property(x => x.PurchasedByOfficerUserId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ApprovedByManagerUserId)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.IbpReferenceNumber).IsUnique();
        builder.HasIndex(x => x.CustomerAccountNumber);
        builder.HasIndex(x => x.CustomerCif);
    }
}
