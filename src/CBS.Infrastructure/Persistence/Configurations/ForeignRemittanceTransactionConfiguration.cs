using System.Text.Json;
using CBS.Domain.Enums;
using CBS.Domain.Remittance;
using CBS.Domain.Remittance.ValueObjects;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ForeignRemittanceTransactionConfiguration : IEntityTypeConfiguration<ForeignRemittanceTransaction>
{
    public void Configure(EntityTypeBuilder<ForeignRemittanceTransaction> builder)
    {
        builder.ToTable("foreign_remittance_transactions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("remittance_transaction_id")
            .IsRequired();

        builder.Property(r => r.TransactionReference)
            .HasColumnName("transaction_reference")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(r => r.TransactionReference)
            .IsUnique();

        builder.Property(r => r.Direction)
            .HasConversion<string>()
            .HasColumnName("direction")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.RemittanceType)
            .HasConversion<string>()
            .HasColumnName("remittance_type")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(r => r.SenderName)
            .HasColumnName("sender_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(r => r.SenderCountry)
            .HasColumnName("sender_country")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(r => r.SenderBankBic)
            .HasColumnName("sender_bank_bic")
            .HasMaxLength(20);

        builder.Property(r => r.BeneficiaryName)
            .HasColumnName("beneficiary_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(r => r.BeneficiaryAccountNumber)
            .HasConversion(v => v != null ? v.Value : null, v => v != null ? AccountNumber.From(v) : null)
            .HasColumnName("beneficiary_account_number")
            .HasMaxLength(13);

        builder.Property(r => r.BeneficiaryCif)
            .HasConversion(v => v != null ? v.Value : null, v => v != null ? new CifNumber(v) : null)
            .HasColumnName("beneficiary_cif")
            .HasMaxLength(30);

        builder.OwnsOne(r => r.ForeignAmount, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("foreign_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("foreign_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(r => r.ExchangeRate)
            .HasColumnName("exchange_rate")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(r => r.ConvertedBdtAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("converted_bdt_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.IncentiveRate)
            .HasColumnName("incentive_rate")
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(r => r.IncentiveAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("incentive_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.TotalDisbursedBdtAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_disbursed_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.ChargeParty)
            .HasConversion<string>()
            .HasColumnName("charge_party")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(r => r.SanctionStatus)
            .HasConversion<string>()
            .HasColumnName("sanction_status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(r => r.IncentiveStatus)
            .HasConversion<string>()
            .HasColumnName("incentive_status")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(r => r.CentralBankClaimBatchId)
            .HasColumnName("central_bank_claim_batch_id");

        builder.Property(r => r.PurposeOfRemittance)
            .HasColumnName("purpose_of_remittance")
            .HasMaxLength(255);

        builder.Property(r => r.QuotaReference)
            .HasColumnName("quota_reference")
            .HasMaxLength(50);

        builder.Property(r => r.QuotaLimitAnnualBdt)
            .HasColumnName("quota_limit_annual_bdt")
            .HasPrecision(18, 2);

        builder.Property(r => r.QuotaUtilizedAnnualBdt)
            .HasColumnName("quota_utilized_annual_bdt")
            .HasPrecision(18, 2);

        builder.Property(r => r.MakerUserId)
            .HasColumnName("maker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.CheckerUserId)
            .HasColumnName("checker_user_id")
            .HasMaxLength(50);

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(r => r.AuthorizedAtUtc)
            .HasColumnName("authorized_at_utc");

        builder.Property(r => r.DisbursedAtUtc)
            .HasColumnName("disbursed_at_utc");

        builder.Property(r => r.CancellationReason)
            .HasColumnName("cancellation_reason")
            .HasMaxLength(255);

        builder.Property(r => r.SwiftMessage)
            .HasConversion(
                v => v != null ? JsonSerializer.Serialize(v, (JsonSerializerOptions?)null) : null,
                v => !string.IsNullOrWhiteSpace(v) ? JsonSerializer.Deserialize<SwiftMt103Payload>(v, (JsonSerializerOptions?)null) : null)
            .HasColumnName("swift_message_json");
    }
}
