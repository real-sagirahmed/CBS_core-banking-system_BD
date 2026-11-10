using CBS.Domain.PaymentRails;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ChequeClearingItemConfiguration : IEntityTypeConfiguration<ChequeClearingItem>
{
    public void Configure(EntityTypeBuilder<ChequeClearingItem> builder)
    {
        builder.ToTable("cheque_clearing_items");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("clearing_item_id")
            .IsRequired();

        builder.Property(c => c.BatchId)
            .HasColumnName("batch_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Direction)
            .HasConversion<string>()
            .HasColumnName("direction")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Session)
            .HasConversion<string>()
            .HasColumnName("session_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Micr)
            .HasConversion(
                v => $"{v.ChequeNumber}|{v.Routing.Value}|{v.Account.Value}|{v.TransactionCode}",
                v => ParseMicrLine(v))
            .HasColumnName("micr_line")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Amount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.PresentingBankRouting)
            .HasConversion(v => v.Value, v => RoutingNumber.From(v))
            .HasColumnName("presenting_bank_routing")
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(c => c.DraweeBankRouting)
            .HasConversion(v => v.Value, v => RoutingNumber.From(v))
            .HasColumnName("drawee_bank_routing")
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(c => c.BeneficiaryAccountNumber)
            .HasColumnName("beneficiary_account_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.ChequeIssueDate)
            .HasConversion(v => v.ToString("yyyy-MM-dd"), v => DateOnly.Parse(v))
            .HasColumnName("cheque_issue_date")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(c => c.PresentationDate)
            .HasConversion(v => v.ToString("yyyy-MM-dd"), v => DateOnly.Parse(v))
            .HasColumnName("presentation_date")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.ReturnReason)
            .HasConversion<string>()
            .HasColumnName("return_reason")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.ReturnRemarks)
            .HasColumnName("return_remarks")
            .HasMaxLength(255);

        builder.Property(c => c.SettlementDate)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString("yyyy-MM-dd") : null,
                v => !string.IsNullOrWhiteSpace(v) ? DateOnly.Parse(v) : (DateOnly?)null)
            .HasColumnName("settlement_date")
            .HasMaxLength(10);
    }

    private static MicrLine ParseMicrLine(string stored)
    {
        var parts = stored.Split('|');
        if (parts.Length == 4)
        {
            return new MicrLine(parts[0], RoutingNumber.From(parts[1]), AccountNumber.From(parts[2]), parts[3]);
        }
        return new MicrLine("000001", RoutingNumber.From("010110001"), AccountNumber.From("0101101000015"), "10");
    }
}
