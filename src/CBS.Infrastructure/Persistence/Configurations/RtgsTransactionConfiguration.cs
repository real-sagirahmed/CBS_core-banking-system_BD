using CBS.Domain.PaymentRails;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class RtgsTransactionConfiguration : IEntityTypeConfiguration<RtgsTransaction>
{
    public void Configure(EntityTypeBuilder<RtgsTransaction> builder)
    {
        builder.ToTable("rtgs_transactions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("rtgs_transaction_id")
            .IsRequired();

        builder.Property(r => r.EndToEndId)
            .HasColumnName("end_to_end_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.MessageType)
            .HasConversion<string>()
            .HasColumnName("message_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Amount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.DebtorAgentRouting)
            .HasConversion(v => v.Value, v => RoutingNumber.From(v))
            .HasColumnName("debtor_agent_routing")
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(r => r.DebtorAccount)
            .HasColumnName("debtor_account")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.CreditorAgentRouting)
            .HasConversion(v => v.Value, v => RoutingNumber.From(v))
            .HasColumnName("creditor_agent_routing")
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(r => r.CreditorAccount)
            .HasColumnName("creditor_account")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.CreditorName)
            .HasColumnName("creditor_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(r => r.RemittanceInfo)
            .HasColumnName("remittance_info")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.BangladeshBankReference)
            .HasColumnName("bangladesh_bank_ref")
            .HasMaxLength(50);

        builder.Property(r => r.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(255);

        builder.Property(r => r.InitiatedAtUtc)
            .HasConversion(v => v.ToUniversalTime().Ticks, v => new DateTime(v, DateTimeKind.Utc))
            .HasColumnName("initiated_at_ticks")
            .IsRequired();

        builder.Property(r => r.SettledAtUtc)
            .HasConversion(
                v => v.HasValue ? v.Value.ToUniversalTime().Ticks : (long?)null,
                v => v.HasValue ? new DateTime(v.Value, DateTimeKind.Utc) : null)
            .HasColumnName("settled_at_ticks");
    }
}
