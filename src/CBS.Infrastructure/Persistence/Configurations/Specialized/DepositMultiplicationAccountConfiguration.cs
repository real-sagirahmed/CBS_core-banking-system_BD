using CBS.Domain.Deposits.Multiplication;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations.Specialized;

public class DepositMultiplicationAccountConfiguration : IEntityTypeConfiguration<DepositMultiplicationAccount>
{
    public void Configure(EntityTypeBuilder<DepositMultiplicationAccount> builder)
    {
        builder.ToTable("deposit_multiplication_accounts");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(m => m.Cif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("cif_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(m => m.InitialDeposit)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("initial_deposit_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(m => m.MultiplierFactor)
            .HasColumnName("multiplier_factor")
            .IsRequired();

        builder.Property(m => m.AnnualInterestRate)
            .HasColumnName("annual_interest_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(m => m.CalculatedTenorMonths)
            .HasColumnName("calculated_tenor_months")
            .IsRequired();

        builder.Property(m => m.MaturityAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("maturity_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(m => m.OpenDate)
            .HasColumnName("open_date")
            .IsRequired();

        builder.Property(m => m.MaturityDate)
            .HasColumnName("maturity_date")
            .IsRequired();

        builder.Property(m => m.IsClosed)
            .HasColumnName("is_closed")
            .IsRequired();

        builder.Property(m => m.ClosedDate)
            .HasColumnName("closed_date");
    }
}
