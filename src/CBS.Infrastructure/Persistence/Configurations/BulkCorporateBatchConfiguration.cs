using CBS.Domain.Corporate;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class BulkCorporateBatchConfiguration : IEntityTypeConfiguration<BulkCorporateBatch>
{
    public void Configure(EntityTypeBuilder<BulkCorporateBatch> builder)
    {
        builder.ToTable("bulk_corporate_batches");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("batch_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.BatchType)
            .HasConversion<string>()
            .HasColumnName("batch_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.CorporateDebitAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("corporate_debit_account")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(b => b.Narration)
            .HasColumnName("narration")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(b => b.ValueDate)
            .HasColumnName("value_date")
            .IsRequired();

        builder.Property(b => b.TotalAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.TotalRecords)
            .HasColumnName("total_records")
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.AuthorizedByUserId)
            .HasColumnName("authorized_by_user_id")
            .HasMaxLength(50);

        builder.Property(b => b.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(b => b.AuthorizedAtUtc)
            .HasColumnName("authorized_at_utc");

        builder.Property(b => b.ExecutedAtUtc)
            .HasColumnName("executed_at_utc");

        builder.Ignore(b => b.DomainEvents);

        builder.HasMany(b => b.Items)
            .WithOne()
            .HasForeignKey(i => i.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.CorporateDebitAccountNumber).HasDatabaseName("ix_bulk_batches_corp_account");
        builder.HasIndex(b => b.Status).HasDatabaseName("ix_bulk_batches_status");
    }
}

public class BulkCorporateBatchItemConfiguration : IEntityTypeConfiguration<BulkCorporateBatchItem>
{
    public void Configure(EntityTypeBuilder<BulkCorporateBatchItem> builder)
    {
        builder.ToTable("bulk_corporate_batch_items");

        builder.HasKey(i => new { i.BatchId, i.ItemIndex });

        builder.Property(i => i.BatchId)
            .HasColumnName("batch_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.ItemIndex)
            .HasColumnName("item_index")
            .IsRequired();

        builder.Property(i => i.BeneficiaryAccountNumber)
            .HasColumnName("beneficiary_account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(i => i.BeneficiaryName)
            .HasColumnName("beneficiary_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(i => i.Amount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.ReferenceNo)
            .HasColumnName("reference_no")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(i => i.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(500);

        builder.Property(i => i.TransactionJournalId)
            .HasColumnName("transaction_journal_id")
            .HasMaxLength(50);

        builder.HasIndex(i => i.BeneficiaryAccountNumber).HasDatabaseName("ix_bulk_batch_items_beneficiary");
    }
}
