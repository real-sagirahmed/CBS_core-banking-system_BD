// ARCHITECTURAL INTENT: EF Core configuration for TelegraphicTransfer aggregate
// enforcing schema, precision, indices, and Money conversion under Bangladesh Bank BRPD Circular 12 (3.13, 1.6.5).

using CBS.Domain.TelegraphicTransfers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TelegraphicTransferConfiguration : IEntityTypeConfiguration<TelegraphicTransfer>
{
    public void Configure(EntityTypeBuilder<TelegraphicTransfer> builder)
    {
        builder.ToTable("telegraphic_transfers");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("tt_id").HasMaxLength(64);

        builder.Property(t => t.TtNumber).HasColumnName("tt_number").HasMaxLength(64).IsRequired();
        builder.Property(t => t.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(t => t.IssuingBranchCode).HasColumnName("issuing_branch_code").HasMaxLength(32).IsRequired();
        builder.Property(t => t.DraweeBranchCode).HasColumnName("drawee_branch_code").HasMaxLength(32).IsRequired();

        builder.OwnsOne(t => t.Amount, a =>
        {
            a.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            a.Property(m => m.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(t => t.PayeeName).HasColumnName("payee_name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.PayeeAccountNumber).HasColumnName("payee_account_number").HasMaxLength(64);
        builder.Property(t => t.RemitterName).HasColumnName("remitter_name").HasMaxLength(200);
        builder.Property(t => t.RemitterAccountNumber).HasColumnName("remitter_account_number").HasMaxLength(64);
        builder.Property(t => t.TransactionPurpose).HasColumnName("transaction_purpose").HasMaxLength(200);

        builder.Property(t => t.CommissionAmount).HasColumnName("commission_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.MessageChargeAmount).HasColumnName("message_charge_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.VatAmount).HasColumnName("vat_amount").HasPrecision(18, 2).IsRequired();

        builder.Property(t => t.TtTestKey).HasColumnName("tt_test_key").HasMaxLength(64).IsRequired();
        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(t => t.SanctionScreeningPassed).HasColumnName("sanction_screening_passed").IsRequired();
        builder.Property(t => t.MakerUserId).HasColumnName("maker_user_id").HasMaxLength(64).IsRequired();
        builder.Property(t => t.CheckerUserId).HasColumnName("checker_user_id").HasMaxLength(64).IsRequired();

        builder.Property(t => t.AdviceTransmittedAtUtc).HasColumnName("advice_transmitted_at_utc");
        builder.Property(t => t.AdviceTransmittedByUserId).HasColumnName("advice_transmitted_by_user_id").HasMaxLength(64);
        builder.Property(t => t.TelephonicallyVerifiedAtUtc).HasColumnName("telephonically_verified_at_utc");
        builder.Property(t => t.TelephonicVerifiedByOfficer).HasColumnName("telephonic_verified_by_officer").HasMaxLength(64);
        builder.Property(t => t.TelephonicContactNumber).HasColumnName("telephonic_contact_number").HasMaxLength(64);
        builder.Property(t => t.TelephonicNotes).HasColumnName("telephonic_notes").HasMaxLength(500);

        builder.Property(t => t.RespondedDate).HasColumnName("responded_date");
        builder.Property(t => t.RespondingAdviceNumber).HasColumnName("responding_advice_number").HasMaxLength(64);
        builder.Property(t => t.RespondingOfficerUserId).HasColumnName("responding_officer_user_id").HasMaxLength(64);
        builder.Property(t => t.RespondingVoucherNumber).HasColumnName("responding_voucher_number").HasMaxLength(64);
        builder.Property(t => t.DestinationAccountOrSundryGl).HasColumnName("destination_account_or_sundry_gl").HasMaxLength(64);

        builder.Property(t => t.CancelledDate).HasColumnName("cancelled_date");
        builder.Property(t => t.CancelledByUserId).HasColumnName("cancelled_by_user_id").HasMaxLength(64);
        builder.Property(t => t.CancellationRemarks).HasColumnName("cancellation_remarks").HasMaxLength(500);

        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(t => t.TtNumber).IsUnique();
        builder.HasIndex(t => t.IssueDate);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.IssuingBranchCode);
        builder.HasIndex(t => t.DraweeBranchCode);
    }
}
