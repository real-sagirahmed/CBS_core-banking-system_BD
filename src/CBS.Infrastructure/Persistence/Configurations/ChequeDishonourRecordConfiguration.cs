// ARCHITECTURAL INTENT: EF Core configuration for ChequeDishonourRecord
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Sections 1.5.2 & 1.5.3.

using CBS.Domain.Clearing;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ChequeDishonourRecordConfiguration : IEntityTypeConfiguration<ChequeDishonourRecord>
{
    public void Configure(EntityTypeBuilder<ChequeDishonourRecord> builder)
    {
        builder.ToTable("cheque_dishonour_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ChequeNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DrawerAccountNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DrawerCif)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.PresenterName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.PresenterBankCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.PresenterBranchCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ChequeAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("cheque_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.DishonourFee)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("dishonour_fee")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.VatAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("vat_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalChargeDebited)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_charge_debited")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ValueType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ReturnReasonCode)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ReturnReasonDescription)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.MemoReferenceNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.NoticeStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.LegalNoticeReference)
            .HasMaxLength(100);

        builder.Property(x => x.SuitCaseNumber)
            .HasMaxLength(100);

        builder.Property(x => x.AdvocateName)
            .HasMaxLength(200);

        builder.Property(x => x.IssuedByUserId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.AuthorizedByCheckerUserId)
            .HasMaxLength(50);

        builder.HasIndex(x => x.DrawerAccountNumber);
        builder.HasIndex(x => x.DrawerCif);
        builder.HasIndex(x => x.MemoReferenceNumber).IsUnique();
        builder.HasIndex(x => x.ChequeNumber);
    }
}
