using System.Text.Json;
using CBS.Domain.BranchCash;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class RemittanceInstrumentConfiguration : IEntityTypeConfiguration<RemittanceInstrument>
{
    public void Configure(EntityTypeBuilder<RemittanceInstrument> builder)
    {
        builder.ToTable("remittance_instruments");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("remittance_instrument_id")
            .IsRequired();

        builder.Property(r => r.InstrumentNumber)
            .HasColumnName("instrument_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Type)
            .HasConversion<string>()
            .HasColumnName("instrument_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(r => r.PurchaserCif)
            .HasColumnName("purchaser_cif")
            .HasMaxLength(20);

        builder.Property(r => r.WalkInDetails)
            .HasConversion(
                v => v != null ? JsonSerializer.Serialize(new { v.FullName, Nid = v.NationalId.Value, v.MobileNumber, v.PurposeOfTransaction }, (JsonSerializerOptions?)null) : null,
                v => !string.IsNullOrWhiteSpace(v) ? ParseWalkIn(v) : null)
            .HasColumnName("walk_in_details_json");

        builder.Property(r => r.BeneficiaryName)
            .HasColumnName("beneficiary_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(r => r.Amount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.Commission)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("commission_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.VatAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("vat_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.TotalPayable)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_payable_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.IssueDate)
            .HasConversion(v => v.ToString("yyyy-MM-dd"), v => DateOnly.Parse(v))
            .HasColumnName("issue_date")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(r => r.MakerUserId)
            .HasColumnName("maker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.CheckerUserId)
            .HasColumnName("checker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.LiquidatedDate)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString("yyyy-MM-dd") : null,
                v => !string.IsNullOrWhiteSpace(v) ? DateOnly.Parse(v) : (DateOnly?)null)
            .HasColumnName("liquidated_date")
            .HasMaxLength(10);

        builder.Property(r => r.LiquidatedBranchCode)
            .HasColumnName("liquidated_branch_code")
            .HasMaxLength(10);

        builder.Property(r => r.StopReason)
            .HasColumnName("stop_reason")
            .HasMaxLength(255);

        builder.Property(r => r.CancellationReason)
            .HasColumnName("cancellation_reason")
            .HasMaxLength(255);

        builder.Property(r => r.DuplicateInstrumentNumber)
            .HasColumnName("duplicate_instrument_number")
            .HasMaxLength(50);

        builder.Property(r => r.IndemnityBondReference)
            .HasColumnName("indemnity_bond_ref")
            .HasMaxLength(50);
    }

    private static WalkInCustomerDetails? ParseWalkIn(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string name = root.GetProperty("FullName").GetString() ?? "";
            string nid = root.GetProperty("Nid").GetString() ?? "19852692511000123";
            string mobile = root.GetProperty("MobileNumber").GetString() ?? "01712345678";
            string purpose = root.GetProperty("PurposeOfTransaction").GetString() ?? "Payment";

            return new WalkInCustomerDetails(name, new NationalIdentityNumber(nid), mobile, purpose);
        }
        catch
        {
            return null;
        }
    }
}
