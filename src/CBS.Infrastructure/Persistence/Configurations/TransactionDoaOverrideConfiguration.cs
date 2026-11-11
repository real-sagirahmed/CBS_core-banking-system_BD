using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TransactionDoaOverrideConfiguration : IEntityTypeConfiguration<TransactionDoaOverride>
{
    public void Configure(EntityTypeBuilder<TransactionDoaOverride> builder)
    {
        builder.ToTable("transaction_doa_overrides");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("override_id")
            .IsRequired();

        builder.Property(o => o.TransactionReference)
            .HasColumnName("transaction_reference")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.AccountNumber)
            .HasColumnName("account_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.InitiatorUserId)
            .HasColumnName("initiator_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.TransactionAmount)
            .HasColumnName("transaction_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.TransactionType)
            .HasColumnName("transaction_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.UserLimit)
            .HasColumnName("user_limit")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.SupervisorUserId)
            .HasColumnName("supervisor_user_id")
            .HasMaxLength(50);

        builder.Property(o => o.AuthorizedAtUtc)
            .HasColumnName("authorized_at_utc");

        builder.Property(o => o.SupervisorRemarks)
            .HasColumnName("supervisor_remarks")
            .HasMaxLength(500);

        builder.Property(o => o.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();
    }
}
