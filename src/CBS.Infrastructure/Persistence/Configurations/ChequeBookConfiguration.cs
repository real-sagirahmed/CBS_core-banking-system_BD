using CBS.Domain.Cheques;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ChequeBookConfiguration : IEntityTypeConfiguration<ChequeBook>
{
    public void Configure(EntityTypeBuilder<ChequeBook> builder)
    {
        builder.ToTable("cheque_books");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("book_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.AccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(b => b.Prefix)
            .HasColumnName("prefix")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(b => b.StartLeafNumber)
            .HasColumnName("start_leaf_number")
            .IsRequired();

        builder.Property(b => b.EndLeafNumber)
            .HasColumnName("end_leaf_number")
            .IsRequired();

        builder.Property(b => b.TotalLeaves)
            .HasColumnName("total_leaves")
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(b => b.RequisitionDate)
            .HasColumnName("requisition_date")
            .IsRequired();

        builder.Property(b => b.DeliveredAtUtc)
            .HasColumnName("delivered_at_utc");

        builder.Property(b => b.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasMany(b => b.Leaves)
            .WithOne()
            .HasForeignKey(l => l.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.Leaves)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(b => b.DomainEvents);

        builder.HasIndex(nameof(ChequeBook.AccountNumber));
        builder.HasIndex(nameof(ChequeBook.Status));
    }
}
