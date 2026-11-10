using CBS.Domain.Cheques;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ChequeLeafConfiguration : IEntityTypeConfiguration<ChequeLeaf>
{
    public void Configure(EntityTypeBuilder<ChequeLeaf> builder)
    {
        builder.ToTable("cheque_leaves");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("leaf_id")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(l => l.BookId)
            .HasColumnName("book_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.AccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(l => l.LeafNumber)
            .HasColumnName("leaf_number")
            .IsRequired();

        builder.Property(l => l.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.StopReason)
            .HasConversion<string>()
            .HasColumnName("stop_reason")
            .HasMaxLength(40);

        builder.Property(l => l.StopNotes)
            .HasColumnName("stop_notes")
            .HasMaxLength(255);

        builder.Property(l => l.StoppedAtUtc)
            .HasColumnName("stopped_at_utc");

        builder.Property(l => l.StoppedByBranch)
            .HasColumnName("stopped_by_branch")
            .HasMaxLength(10);

        builder.Property(l => l.RevokedByOfficerId)
            .HasColumnName("revoked_by_officer_id")
            .HasMaxLength(50);

        builder.Property(l => l.RevokedAtUtc)
            .HasColumnName("revoked_at_utc");

        builder.Property(l => l.PaidAtUtc)
            .HasColumnName("paid_at_utc");

        builder.Property(l => l.PaidVoucherNumber)
            .HasColumnName("paid_voucher_number")
            .HasMaxLength(50);

        builder.HasIndex(nameof(ChequeLeaf.AccountNumber), nameof(ChequeLeaf.LeafNumber)).IsUnique();
        builder.HasIndex(nameof(ChequeLeaf.Status));
    }
}
