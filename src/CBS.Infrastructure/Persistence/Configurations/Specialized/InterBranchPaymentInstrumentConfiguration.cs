using CBS.Domain.BranchCash;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations.Specialized;

public class InterBranchPaymentInstrumentConfiguration : IEntityTypeConfiguration<InterBranchPaymentInstrument>
{
    public void Configure(EntityTypeBuilder<InterBranchPaymentInstrument> builder)
    {
        builder.ToTable("inter_branch_payment_instruments");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("instrument_id")
            .IsRequired();

        builder.Property(i => i.InstrumentNumber)
            .HasColumnName("instrument_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.Type)
            .HasConversion<string>()
            .HasColumnName("instrument_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(i => i.Amount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.Commission)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("commission_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.VatAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("vat_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.IssuingBranchCode)
            .HasColumnName("issuing_branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(i => i.PayeeBranchCode)
            .HasColumnName("payee_branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(i => i.BeneficiaryName)
            .HasColumnName("beneficiary_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(i => i.BeneficiaryAccountNumber)
            .HasColumnName("beneficiary_account_number")
            .HasMaxLength(20);

        builder.Property(i => i.RemitterName)
            .HasColumnName("remitter_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(i => i.IssueDate)
            .HasColumnName("issue_date")
            .IsRequired();

        builder.Property(i => i.CryptographicTestKey)
            .HasColumnName("cryptographic_test_key")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(i => i.IsSanctionScreened)
            .HasColumnName("is_sanction_screened")
            .IsRequired();

        builder.Property(i => i.ContraEntryPosted)
            .HasColumnName("contra_entry_posted")
            .IsRequired();

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(i => i.PaidDate)
            .HasColumnName("paid_date");

        builder.Property(i => i.PaidByUserId)
            .HasColumnName("paid_by_user_id")
            .HasMaxLength(50);

        builder.Property(i => i.CancelledByUserId)
            .HasColumnName("cancelled_by_user_id")
            .HasMaxLength(50);
    }
}
