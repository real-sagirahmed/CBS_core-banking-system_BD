using System.Text.Json;
using CBS.Domain.BranchCash;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class SecurityDepositReceiptConfiguration : IEntityTypeConfiguration<SecurityDepositReceipt>
{
    public void Configure(EntityTypeBuilder<SecurityDepositReceipt> builder)
    {
        builder.ToTable("security_deposit_receipts");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("sdr_id")
            .IsRequired();

        builder.Property(s => s.SdrNumber)
            .HasColumnName("sdr_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(s => s.PurchaserCif)
            .HasColumnName("purchaser_cif")
            .HasMaxLength(20);

        builder.Property(s => s.WalkInDetails)
            .HasConversion(
                v => v != null ? JsonSerializer.Serialize(new { v.FullName, Nid = v.NationalId.Value, v.MobileNumber, v.PurposeOfTransaction }, (JsonSerializerOptions?)null) : null,
                v => !string.IsNullOrWhiteSpace(v) ? ParseWalkIn(v) : null)
            .HasColumnName("walk_in_details_json");

        builder.Property(s => s.TenderOrRfpNumber)
            .HasColumnName("tender_rfp_number")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.BeneficiaryAuthority)
            .HasColumnName("beneficiary_authority")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Amount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.Commission)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("commission_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.VatAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("vat_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.TotalPayable)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("total_payable_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.IssueDate)
            .HasColumnName("issue_date")
            .IsRequired();

        builder.Property(s => s.ExpiryDate)
            .HasColumnName("expiry_date")
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.MakerUserId)
            .HasColumnName("maker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.CheckerUserId)
            .HasColumnName("checker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.ReleaseLetterReference)
            .HasColumnName("release_letter_ref")
            .HasMaxLength(100);

        builder.Property(s => s.ForfeitureClaimReference)
            .HasColumnName("forfeiture_claim_ref")
            .HasMaxLength(100);

        builder.Property(s => s.SettledDate)
            .HasColumnName("settled_date");

        builder.Property(s => s.StopReason)
            .HasColumnName("stop_reason")
            .HasMaxLength(255);

        builder.Property(s => s.StopRevocationReason)
            .HasColumnName("stop_revocation_reason")
            .HasMaxLength(255);

        builder.Property(s => s.DuplicateSdrNumber)
            .HasColumnName("duplicate_sdr_number")
            .HasMaxLength(50);

        builder.Property(s => s.IndemnityBondReference)
            .HasColumnName("indemnity_bond_ref")
            .HasMaxLength(100);

        builder.HasIndex(s => s.SdrNumber).IsUnique();
        builder.HasIndex(s => s.BranchCode);
        builder.HasIndex(s => s.TenderOrRfpNumber);
        builder.HasIndex(s => s.Status);
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
            string purpose = root.GetProperty("PurposeOfTransaction").GetString() ?? "Tender Security Deposit";

            return new WalkInCustomerDetails(name, new NationalIdentityNumber(nid), mobile, purpose);
        }
        catch
        {
            return null;
        }
    }
}
