using CBS.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class UnclaimedDepositItemConfiguration : IEntityTypeConfiguration<UnclaimedDepositItem>
{
    public void Configure(EntityTypeBuilder<UnclaimedDepositItem> builder)
    {
        builder.ToTable("unclaimed_deposit_items");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("item_id")
            .IsRequired();

        builder.Property(u => u.AccountNumber)
            .HasColumnName("account_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.CustomerCif)
            .HasColumnName("customer_cif")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.CustomerName)
            .HasColumnName("customer_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.AccountType)
            .HasConversion<string>()
            .HasColumnName("account_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.CurrentBalance)
            .HasColumnName("current_balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(u => u.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(u => u.LastTransactionDate)
            .HasColumnName("last_transaction_date")
            .IsRequired();

        builder.Property(u => u.InoperativeDays)
            .HasColumnName("inoperative_days")
            .IsRequired();

        builder.Property(u => u.UnclaimedYears)
            .HasColumnName("unclaimed_years")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.NoticeIssuedDate)
            .HasColumnName("notice_issued_date");

        builder.Property(u => u.NoticeReference)
            .HasColumnName("notice_reference")
            .HasMaxLength(100);

        builder.Property(u => u.NoticeExpirationDate)
            .HasColumnName("notice_expiration_date");

        builder.Property(u => u.TransferScheduleId)
            .HasColumnName("transfer_schedule_id")
            .HasMaxLength(100);

        builder.Property(u => u.RemittedToCentralBankDate)
            .HasColumnName("remitted_to_central_bank_date");

        builder.Property(u => u.CentralBankReceiptRef)
            .HasColumnName("central_bank_receipt_ref")
            .HasMaxLength(100);

        builder.Property(u => u.ReclaimedDate)
            .HasColumnName("reclaimed_date");

        builder.Property(u => u.ReclaimedByAuthorizedUserId)
            .HasColumnName("reclaimed_by_authorized_user_id")
            .HasMaxLength(50);

        builder.Property(u => u.ReclaimReason)
            .HasColumnName("reclaim_reason")
            .HasMaxLength(500);

        builder.HasIndex(u => u.AccountNumber)
            .IsUnique()
            .HasDatabaseName("ix_unclaimed_deposits_account");

        builder.HasIndex(u => u.Status)
            .HasDatabaseName("ix_unclaimed_deposits_status");
    }
}
