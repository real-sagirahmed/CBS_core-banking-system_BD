using System.Text.Json;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class MasterExportLcConfiguration : IEntityTypeConfiguration<MasterExportLc>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public void Configure(EntityTypeBuilder<MasterExportLc> builder)
    {
        builder.ToTable("master_export_lcs");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("master_lc_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.ContractType)
            .HasConversion<string>()
            .HasColumnName("contract_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.ExporterCif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("exporter_cif")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.IssuingBankBic)
            .HasColumnName("issuing_bank_bic")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.IssuingBankName)
            .HasColumnName("issuing_bank_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.BuyerName)
            .HasColumnName("buyer_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.BuyerCountry)
            .HasColumnName("buyer_country")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(m => m.TotalAmount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("total_amount_json")
            .IsRequired();

        builder.Property(m => m.ValueAdditionPercentage)
            .HasColumnName("value_addition_percentage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(m => m.CumulativeB2bAllocatedAmount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("cumulative_b2b_allocated_amount_json")
            .IsRequired();

        builder.Property(m => m.IssueDate)
            .HasColumnName("issue_date")
            .IsRequired();

        builder.Property(m => m.ExpiryDate)
            .HasColumnName("expiry_date")
            .IsRequired();

        builder.Property(m => m.LatestShipmentDate)
            .HasColumnName("latest_shipment_date")
            .IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(m => m.MakerUserId)
            .HasColumnName("maker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.CheckerUserId)
            .HasColumnName("checker_user_id")
            .HasMaxLength(50);

        builder.Property(m => m.AuthorizedAtUtc)
            .HasColumnName("authorized_at_utc");

        builder.Property(m => m.ReplacedContractRef)
            .HasColumnName("replaced_contract_ref")
            .HasMaxLength(50);

        builder.Ignore(m => m.MaxPermissibleB2bAmount);
        builder.Ignore(m => m.RemainingB2bCapacity);
        builder.Ignore(m => m.DomainEvents);
    }
}
