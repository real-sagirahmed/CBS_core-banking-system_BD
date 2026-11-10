using System.Text.Json;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ShippingGuaranteeConfiguration : IEntityTypeConfiguration<ShippingGuarantee>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Configure(EntityTypeBuilder<ShippingGuarantee> builder)
    {
        builder.ToTable("trade_shipping_guarantees");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasColumnName("sg_reference")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(g => g.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(g => g.CustomerDepositAccountNumber)
            .HasColumnName("customer_deposit_account_number")
            .HasMaxLength(20);

        builder.Property(g => g.LcNumber)
            .HasColumnName("lc_number")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(g => g.ApplicantId)
            .HasColumnName("applicant_id")
            .IsRequired();

        builder.Property(g => g.CarrierName)
            .HasColumnName("carrier_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(g => g.VesselName)
            .HasColumnName("vessel_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(g => g.PortOfDischarge)
            .HasColumnName("port_of_discharge")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(g => g.GuaranteeAmount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("guarantee_amount_json")
            .IsRequired();

        builder.Property(g => g.CashMarginAmount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("cash_margin_json")
            .IsRequired();

        builder.Property(g => g.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(g => g.SurrenderedOriginalBlNumber)
            .HasColumnName("surrendered_original_bl_number")
            .HasMaxLength(100);

        builder.Property(g => g.OriginalBlSurrenderedAtUtc)
            .HasColumnName("original_bl_surrendered_at_utc");

        builder.Property(g => g.MakerUserId)
            .HasColumnName("maker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(g => g.CheckerUserId)
            .HasColumnName("checker_user_id")
            .HasMaxLength(50);

        builder.Property(g => g.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(g => g.AuthorizedAtUtc)
            .HasColumnName("authorized_at_utc");

        builder.Ignore(g => g.DomainEvents);

        builder.HasIndex(g => g.BranchCode).HasDatabaseName("ix_trade_sg_branch");
        builder.HasIndex(g => g.LcNumber).HasDatabaseName("ix_trade_sg_lc");
        builder.HasIndex(g => g.Status).HasDatabaseName("ix_trade_sg_status");
    }
}
