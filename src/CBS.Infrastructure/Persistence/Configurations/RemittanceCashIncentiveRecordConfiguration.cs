using CBS.Domain.Remittance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class RemittanceCashIncentiveRecordConfiguration : IEntityTypeConfiguration<RemittanceCashIncentiveRecord>
{
    public void Configure(EntityTypeBuilder<RemittanceCashIncentiveRecord> builder)
    {
        builder.ToTable("remittance_cash_incentives");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("incentive_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.RemittanceReferenceNumber)
            .HasColumnName("remittance_reference_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.ExchangeHouseCode)
            .HasColumnName("exchange_house_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.RemitterName)
            .HasColumnName("remitter_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.RemitterCountry)
            .HasColumnName("remitter_country")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.BeneficiaryAccountNumber)
            .HasColumnName("beneficiary_account_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.BeneficiaryCif)
            .HasColumnName("beneficiary_cif")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.RemittanceAmountBdt)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("remittance_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.IncentiveRate)
            .HasColumnName("incentive_rate")
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(r => r.IncentiveAmountBdt)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("incentive_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.TotalDisbursedBdt)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_disbursed_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.DisbursingBranchCode)
            .HasColumnName("disbursing_branch_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.DisbursedAtUtc)
            .HasColumnName("disbursed_at_utc")
            .IsRequired();

        builder.Property(r => r.DisbursedByUserId)
            .HasColumnName("disbursed_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.ClaimStatus)
            .HasConversion<string>()
            .HasColumnName("claim_status")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(r => r.ClaimBatchReference)
            .HasColumnName("claim_batch_reference")
            .HasMaxLength(50);

        builder.Property(r => r.ClaimSubmittedAtUtc)
            .HasColumnName("claim_submitted_at_utc");

        builder.Property(r => r.ReimbursedAtUtc)
            .HasColumnName("reimbursed_at_utc");

        builder.Property(r => r.CentralBankVoucherReference)
            .HasColumnName("central_bank_voucher_reference")
            .HasMaxLength(50);
    }
}
