// ARCHITECTURAL INTENT: EF Core configuration for PaymentInstrument aggregate.
// Enforces precise column mapping, indices, and Money conversion under Bangladesh Bank BRPD Circular 12/2024.

using CBS.Domain.PaymentInstruments;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class PaymentInstrumentConfiguration : IEntityTypeConfiguration<PaymentInstrument>
{
    public void Configure(EntityTypeBuilder<PaymentInstrument> builder)
    {
        builder.ToTable("payment_instruments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("instrument_id").HasMaxLength(64);

        builder.Property(p => p.InstrumentType)
            .HasColumnName("instrument_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.InstrumentNumber)
            .HasColumnName("instrument_number")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(p => p.IssuingBranchCode)
            .HasColumnName("issuing_branch_code")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.DraweeBranchCode)
            .HasColumnName("drawee_branch_code")
            .HasMaxLength(32);

        builder.OwnsOne(p => p.Amount, a =>
        {
            a.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            a.Property(m => m.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(p => p.PayeeName).HasColumnName("payee_name").HasMaxLength(200).IsRequired();
        builder.Property(p => p.RemitterName).HasColumnName("remitter_name").HasMaxLength(200).IsRequired();
        builder.Property(p => p.RemitterAccountNumber).HasColumnName("remitter_account_number").HasMaxLength(64);

        builder.Property(p => p.IsWalkInCustomer).HasColumnName("is_walk_in_customer").IsRequired();
        builder.Property(p => p.WalkInNid).HasColumnName("walk_in_nid").HasMaxLength(64);
        builder.Property(p => p.WalkInPhotoId).HasColumnName("walk_in_photo_id").HasMaxLength(64);
        builder.Property(p => p.WalkInMobile).HasColumnName("walk_in_mobile").HasMaxLength(32);
        builder.Property(p => p.TransactionPurpose).HasColumnName("transaction_purpose").HasMaxLength(200).IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.CommissionAmount).HasColumnName("commission_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.VatAmount).HasColumnName("vat_amount").HasPrecision(18, 2).IsRequired();

        builder.Property(p => p.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(p => p.IssuedByUserId).HasColumnName("issued_by_user_id").HasMaxLength(64).IsRequired();
        builder.Property(p => p.AuthorizedByUserId).HasColumnName("authorized_by_user_id").HasMaxLength(64).IsRequired();

        builder.Property(p => p.DdTestKey).HasColumnName("dd_test_key").HasMaxLength(64);
        builder.Property(p => p.DrawingAdviceTransmitted).HasColumnName("drawing_advice_transmitted").IsRequired();

        builder.Property(p => p.PaidDate).HasColumnName("paid_date");
        builder.Property(p => p.PaidByUserId).HasColumnName("paid_by_user_id").HasMaxLength(64);
        builder.Property(p => p.PaidVoucherNumber).HasColumnName("paid_voucher_number").HasMaxLength(64);

        builder.Property(p => p.CancelledDate).HasColumnName("cancelled_date");
        builder.Property(p => p.CancelledByUserId).HasColumnName("cancelled_by_user_id").HasMaxLength(64);
        builder.Property(p => p.CancellationRemarks).HasColumnName("cancellation_remarks").HasMaxLength(500);
        builder.Property(p => p.CancellationFeeDeducted).HasColumnName("cancellation_fee_deducted").HasPrecision(18, 2);

        builder.Property(p => p.PoliceGdNumber).HasColumnName("police_gd_number").HasMaxLength(64);
        builder.Property(p => p.IndemnityBondNumber).HasColumnName("indemnity_bond_number").HasMaxLength(64);
        builder.Property(p => p.DuplicateOfInstrumentId).HasColumnName("duplicate_of_instrument_id").HasMaxLength(64);

        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(p => p.InstrumentNumber).IsUnique();
        builder.HasIndex(p => p.IssuingBranchCode);
        builder.HasIndex(p => p.DraweeBranchCode);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.IssueDate);
        builder.HasIndex(p => p.InstrumentType);
    }
}
