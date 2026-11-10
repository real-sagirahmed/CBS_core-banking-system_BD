using CBS.Domain.TermDeposits;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TermDepositContractConfiguration : IEntityTypeConfiguration<TermDepositContract>
{
    public void Configure(EntityTypeBuilder<TermDepositContract> builder)
    {
        builder.ToTable("term_deposit_contracts");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("contract_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.FdrAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("fdr_account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(t => t.OperativeAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("operative_account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.OwnsOne(t => t.PrincipalAmount, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("principal_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("principal_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(t => t.TenorMonths)
            .HasColumnName("tenor_months")
            .IsRequired();

        builder.Property(t => t.AnnualInterestRate)
            .HasColumnName("annual_interest_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(t => t.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(t => t.MaturityDate)
            .HasColumnName("maturity_date")
            .IsRequired();

        builder.Property(t => t.MaturityInstruction)
            .HasConversion<string>()
            .HasColumnName("maturity_instruction")
            .HasMaxLength(40)
            .IsRequired();

        builder.OwnsOne(t => t.AccruedInterestProvision, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("accrued_provision_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("accrued_provision_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(t => t.LastInterestCalculationDate)
            .HasColumnName("last_interest_calc_date");

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.IsLienMarked)
            .HasColumnName("is_lien_marked")
            .IsRequired();

        builder.OwnsOne(t => t.LienAmount, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("lien_amount")
                .HasPrecision(18, 2);

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("lien_currency")
                .HasMaxLength(3);
        });

        builder.Property(t => t.LienReference)
            .HasColumnName("lien_reference")
            .HasMaxLength(100);

        builder.Property(t => t.ClosureDate)
            .HasColumnName("closure_date");

        builder.OwnsOne(t => t.PaidInterestAmount, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("paid_interest_amount")
                .HasPrecision(18, 2);

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("paid_interest_currency")
                .HasMaxLength(3);
        });

        builder.OwnsOne(t => t.WithholdingTaxAmount, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("withholding_tax_amount")
                .HasPrecision(18, 2);

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("withholding_tax_currency")
                .HasMaxLength(3);
        });

        builder.Property(t => t.PenalInterestMarkdown)
            .HasColumnName("penal_interest_markdown")
            .HasPrecision(5, 2);

        builder.Property(t => t.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Ignore(t => t.DomainEvents);

        builder.HasIndex(nameof(TermDepositContract.FdrAccountNumber));
        builder.HasIndex(nameof(TermDepositContract.OperativeAccountNumber));
        builder.HasIndex(nameof(TermDepositContract.MaturityDate));
        builder.HasIndex(nameof(TermDepositContract.Status));
    }
}
