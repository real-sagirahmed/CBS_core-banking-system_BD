using CBS.Domain.BackValue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class BackValueAuditJournalEntryConfiguration : IEntityTypeConfiguration<BackValueAuditJournalEntry>
{
    public void Configure(EntityTypeBuilder<BackValueAuditJournalEntry> builder)
    {
        builder.ToTable("back_value_audit_journal_entries");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnName("entry_id").HasMaxLength(64);

        builder.Property(j => j.RequestId).HasColumnName("request_id").HasMaxLength(64).IsRequired();
        builder.Property(j => j.BranchCode).HasColumnName("branch_code").HasMaxLength(32).IsRequired();
        builder.Property(j => j.BookingDate).HasColumnName("booking_date").IsRequired();
        builder.Property(j => j.ValueDate).HasColumnName("value_date").IsRequired();
        builder.Property(j => j.DaysVariance).HasColumnName("days_variance").IsRequired();
        builder.Property(j => j.TargetAccountOrGl).HasColumnName("target_account_or_gl").HasMaxLength(64).IsRequired();
        builder.Property(j => j.PrincipalAmount).HasColumnName("principal_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(j => j.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired();
        builder.Property(j => j.RetroactiveInterestAmount).HasColumnName("retroactive_interest_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(j => j.Direction).HasColumnName("direction").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(j => j.MakerUserId).HasColumnName("maker_user_id").HasMaxLength(64).IsRequired();
        builder.Property(j => j.CheckerUserId).HasColumnName("checker_user_id").HasMaxLength(64).IsRequired();
        builder.Property(j => j.ApprovalTimeUtc).HasColumnName("approval_time_utc").IsRequired();
        builder.Property(j => j.VoucherNumber).HasColumnName("voucher_number").HasMaxLength(64).IsRequired();
        builder.Property(j => j.AuditNarration).HasColumnName("audit_narration").HasMaxLength(500).IsRequired();

        builder.HasIndex(j => j.BookingDate);
        builder.HasIndex(j => j.ValueDate);
        builder.HasIndex(j => j.BranchCode);
    }
}
