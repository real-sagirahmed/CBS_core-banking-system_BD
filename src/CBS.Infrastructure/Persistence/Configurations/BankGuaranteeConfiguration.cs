using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class BankGuaranteeConfiguration : IEntityTypeConfiguration<BankGuarantee>
{
    public void Configure(EntityTypeBuilder<BankGuarantee> builder)
    {
        builder.ToTable("bank_guarantees");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("bank_guarantee_id")
            .IsRequired();

        builder.Property(b => b.BgNumber)
            .HasColumnName("bg_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.Type)
            .HasConversion<string>()
            .HasColumnName("guarantee_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.ApplicantCif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("applicant_cif")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.BeneficiaryName)
            .HasColumnName("beneficiary_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(b => b.BeneficiaryAddress)
            .HasColumnName("beneficiary_address")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(b => b.IssuingBranchCode)
            .HasColumnName("issuing_branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(b => b.GuaranteeAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("guarantee_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.CashMarginAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("cash_margin_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.CollateralDepositAccountNumber)
            .HasColumnName("collateral_deposit_account")
            .HasMaxLength(20);

        builder.Property(b => b.MarginPercentage)
            .HasColumnName("margin_percentage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(b => b.CommissionRatePerQuarter)
            .HasColumnName("commission_rate_per_quarter")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(b => b.CommissionAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("commission_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.VatAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("vat_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.TotalFeePayable)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_fee_payable_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.IssueDate)
            .HasColumnName("issue_date")
            .IsRequired();

        builder.Property(b => b.ExpiryDate)
            .HasColumnName("expiry_date")
            .IsRequired();

        builder.Property(b => b.ClaimPeriodDays)
            .HasColumnName("claim_period_days")
            .IsRequired();

        builder.Property(b => b.ExpiryType)
            .HasConversion<string>()
            .HasColumnName("expiry_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.MakerUserId)
            .HasColumnName("maker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.CheckerUserId)
            .HasColumnName("checker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.MisSectorCode)
            .HasColumnName("mis_sector_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.AmendmentReference)
            .HasColumnName("amendment_ref")
            .HasMaxLength(100);

        builder.Property(b => b.ClaimLetterReference)
            .HasColumnName("claim_letter_ref")
            .HasMaxLength(100);

        builder.Property(b => b.ClaimDate)
            .HasColumnName("claim_date");

        builder.Property(b => b.ReleaseLetterReference)
            .HasColumnName("release_letter_ref")
            .HasMaxLength(100);

        builder.Property(b => b.OriginalReturned)
            .HasColumnName("original_returned")
            .IsRequired();

        builder.Property(b => b.CancelledDate)
            .HasColumnName("cancelled_date");

        builder.HasIndex(b => b.BgNumber).IsUnique();
        builder.HasIndex(b => b.IssuingBranchCode);
        builder.HasIndex(b => b.Status);
    }
}
