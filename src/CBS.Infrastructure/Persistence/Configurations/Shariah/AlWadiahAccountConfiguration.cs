using CBS.Domain.Shariah;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations.Shariah;

public class AlWadiahAccountConfiguration : IEntityTypeConfiguration<AlWadiahAccount>
{
    public void Configure(EntityTypeBuilder<AlWadiahAccount> builder)
    {
        builder.ToTable("al_wadiah_accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(a => a.AccountNumber)
            .HasColumnName("account_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Cif)
            .HasColumnName("cif")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(a => a.Balance)
            .HasColumnName("balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.InterestRate)
            .HasColumnName("interest_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(a => a.SafeCustodyContractRef)
            .HasColumnName("safe_custody_contract_ref")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(a => a.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(a => a.AccountNumber)
            .IsUnique()
            .HasDatabaseName("ux_al_wadiah_account_number");

        builder.HasIndex(a => a.Cif)
            .HasDatabaseName("ix_al_wadiah_cif");
    }
}
