using System.Text.Json;
using CBS.Domain.Treasury;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TreasuryDealConfiguration : IEntityTypeConfiguration<TreasuryDeal>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Configure(EntityTypeBuilder<TreasuryDeal> builder)
    {
        builder.ToTable("treasury_deals");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("deal_reference")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(d => d.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(d => d.Type)
            .HasConversion<string>()
            .HasColumnName("deal_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.Direction)
            .HasConversion<string>()
            .HasColumnName("direction")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.CounterpartyBankBic)
            .HasColumnName("counterparty_bank_bic")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.CounterpartyName)
            .HasColumnName("counterparty_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.PrimaryAmount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("primary_amount_json")
            .IsRequired();

        builder.Property(d => d.SecondaryAmount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("secondary_amount_json")
            .IsRequired();

        builder.Property(d => d.ExchangeRate)
            .HasColumnName("exchange_rate")
            .HasPrecision(18, 6);

        builder.Property(d => d.InterestRate)
            .HasColumnName("interest_rate")
            .HasPrecision(7, 4);

        builder.Property(d => d.DealDate)
            .HasColumnName("deal_date")
            .IsRequired();

        builder.Property(d => d.ValueDate)
            .HasColumnName("value_date")
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.FrontOfficeDealerUserId)
            .HasColumnName("front_office_dealer_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.MidOfficeRiskOfficerUserId)
            .HasColumnName("mid_office_risk_officer_user_id")
            .HasMaxLength(50);

        builder.Property(d => d.MidOfficeVerifiedAtUtc)
            .HasColumnName("mid_office_verified_at_utc");

        builder.Property(d => d.BackOfficeSettlementUserId)
            .HasColumnName("back_office_settlement_user_id")
            .HasMaxLength(50);

        builder.Property(d => d.SettledAtUtc)
            .HasColumnName("settled_at_utc");

        builder.Property(d => d.SettlementVoucherNumber)
            .HasColumnName("settlement_voucher_number")
            .HasMaxLength(64);

        builder.Property(d => d.SecurityIsin)
            .HasColumnName("security_isin")
            .HasMaxLength(30);
    }
}
