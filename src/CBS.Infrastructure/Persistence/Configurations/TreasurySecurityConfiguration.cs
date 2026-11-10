using System.Text.Json;
using CBS.Domain.Treasury;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TreasurySecurityConfiguration : IEntityTypeConfiguration<TreasurySecurity>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Configure(EntityTypeBuilder<TreasurySecurity> builder)
    {
        builder.ToTable("treasury_securities");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("security_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(s => s.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(s => s.SecurityName)
            .HasColumnName("security_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.SecurityType)
            .HasConversion<string>()
            .HasColumnName("security_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.Classification)
            .HasConversion<string>()
            .HasColumnName("classification")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.FaceValue)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("face_value_json")
            .IsRequired();

        builder.Property(s => s.BookValue)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("book_value_json")
            .IsRequired();

        builder.Property(s => s.MarketValue)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("market_value_json")
            .IsRequired();

        builder.Property(s => s.CouponRate)
            .HasColumnName("coupon_rate")
            .HasPrecision(7, 4);

        builder.Property(s => s.PurchaseYield)
            .HasColumnName("purchase_yield")
            .HasPrecision(7, 4);

        builder.Property(s => s.CurrentMarketYield)
            .HasColumnName("current_market_yield")
            .HasPrecision(7, 4);

        builder.Property(s => s.CleanPrice)
            .HasColumnName("clean_price")
            .HasPrecision(10, 4);

        builder.Property(s => s.IssueDate)
            .HasColumnName("issue_date")
            .IsRequired();

        builder.Property(s => s.MaturityDate)
            .HasColumnName("maturity_date")
            .IsRequired();

        builder.Property(s => s.LastMtmValuationDate)
            .HasColumnName("last_mtm_valuation_date")
            .IsRequired();

        builder.Property(s => s.UnrealizedMtmGainLoss)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("unrealized_mtm_gain_loss_json")
            .IsRequired();
    }
}
