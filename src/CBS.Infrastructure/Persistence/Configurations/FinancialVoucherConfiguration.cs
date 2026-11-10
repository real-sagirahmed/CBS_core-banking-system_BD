using System.Text.Json;
using CBS.Domain.GeneralLedger;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class FinancialVoucherConfiguration : IEntityTypeConfiguration<FinancialVoucher>
{
    public void Configure(EntityTypeBuilder<FinancialVoucher> builder)
    {
        builder.ToTable("financial_vouchers");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("id");

        builder.Property(v => v.VoucherNumber)
            .HasColumnName("voucher_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(v => v.BranchSol)
            .HasColumnName("branch_sol")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(v => v.Type)
            .HasConversion<string>()
            .HasColumnName("voucher_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(v => v.ValueDate)
            .HasColumnName("value_date")
            .IsRequired();

        builder.Property(v => v.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasColumnName("voucher_status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(v => v.Audit)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<MakerCheckerAudit>(v, (JsonSerializerOptions?)null)!)
            .HasColumnName("audit_json")
            .IsRequired();

        builder.Property(v => v.ReversalOfVoucherId)
            .HasColumnName("reversal_of_voucher_id");

        builder.Ignore(v => v.DomainEvents);

        // One-to-many relationship with entries
        builder.HasMany(v => v.Entries)
            .WithOne()
            .HasForeignKey("VoucherId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(v => v.Entries)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(v => v.VoucherNumber).IsUnique().HasDatabaseName("ux_financial_vouchers_number");
        builder.HasIndex(v => v.BranchSol).HasDatabaseName("ix_financial_vouchers_branch");
        builder.HasIndex(v => v.ValueDate).HasDatabaseName("ix_financial_vouchers_date");
        builder.HasIndex(v => v.Status).HasDatabaseName("ix_financial_vouchers_status");
    }
}

public class VoucherEntryConfiguration : IEntityTypeConfiguration<VoucherEntry>
{
    public void Configure(EntityTypeBuilder<VoucherEntry> builder)
    {
        builder.ToTable("voucher_entries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property<Guid>("VoucherId")
            .HasColumnName("voucher_id")
            .IsRequired();

        builder.Property(e => e.AccountCode)
            .HasColumnName("account_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasColumnName("entry_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.ExchangeRate)
            .HasColumnName("exchange_rate")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(e => e.BaseAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("base_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.Narration)
            .HasColumnName("narration")
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex("VoucherId").HasDatabaseName("ix_voucher_entries_voucher_id");
        builder.HasIndex(e => e.AccountCode).HasDatabaseName("ix_voucher_entries_account_code");
    }
}
