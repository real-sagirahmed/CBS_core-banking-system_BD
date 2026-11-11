using CBS.Domain.Lending.Discounted;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations.Specialized;

public class BillDiscountingFacilityConfiguration : IEntityTypeConfiguration<BillDiscountingFacility>
{
    public void Configure(EntityTypeBuilder<BillDiscountingFacility> builder)
    {
        builder.ToTable("bill_discounting_facilities");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(b => b.Cif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("cif_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.BillReferenceNumber)
            .HasColumnName("bill_reference_number")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.DraweeBankRouting)
            .HasColumnName("drawee_bank_routing")
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(b => b.DraweeBankName)
            .HasColumnName("drawee_bank_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(b => b.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(b => b.BillFaceValue)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("bill_face_value_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.DiscountRatePercent)
            .HasColumnName("discount_rate_percent")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(b => b.TenorDays)
            .HasColumnName("tenor_days")
            .IsRequired();

        builder.Property(b => b.UpfrontDiscountAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("upfront_discount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.NetDisbursedAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("net_disbursed_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.UnearnedDiscountBalance)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("unearned_discount_balance_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.RecognizedPnLInterest)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("recognized_pnl_interest_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.PurchaseDate)
            .HasColumnName("purchase_date")
            .IsRequired();

        builder.Property(b => b.MaturityDate)
            .HasColumnName("maturity_date")
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();
    }
}
