using CBS.Domain.PaymentRails;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class BeftnBatchItemConfiguration : IEntityTypeConfiguration<BeftnBatchItem>
{
    public void Configure(EntityTypeBuilder<BeftnBatchItem> builder)
    {
        builder.ToTable("beftn_batch_items");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("beftn_item_id")
            .IsRequired();

        builder.Property(b => b.BatchId)
            .HasColumnName("batch_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.Direction)
            .HasConversion<string>()
            .HasColumnName("direction")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.Type)
            .HasConversion<string>()
            .HasColumnName("transaction_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.Amount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.OriginatingRouting)
            .HasConversion(v => v.Value, v => RoutingNumber.From(v))
            .HasColumnName("originating_routing")
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(b => b.OriginatingAccount)
            .HasColumnName("originating_account")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.DestinationRouting)
            .HasConversion(v => v.Value, v => RoutingNumber.From(v))
            .HasColumnName("destination_routing")
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(b => b.DestinationAccount)
            .HasColumnName("destination_account")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.BeneficiaryName)
            .HasColumnName("beneficiary_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(b => b.PaymentReference)
            .HasColumnName("payment_reference")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.SubmissionDate)
            .HasConversion(v => v.ToString("yyyy-MM-dd"), v => DateOnly.Parse(v))
            .HasColumnName("submission_date")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(b => b.SettlementDate)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString("yyyy-MM-dd") : null,
                v => !string.IsNullOrWhiteSpace(v) ? DateOnly.Parse(v) : (DateOnly?)null)
            .HasColumnName("settlement_date")
            .HasMaxLength(10);

        builder.Property(b => b.ReturnCode)
            .HasColumnName("return_code")
            .HasMaxLength(20);

        builder.Property(b => b.ReturnReason)
            .HasColumnName("return_reason")
            .HasMaxLength(255);
    }
}
