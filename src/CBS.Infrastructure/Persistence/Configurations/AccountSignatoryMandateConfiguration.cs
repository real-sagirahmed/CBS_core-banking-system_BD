using System.Text.Json;
using CBS.Domain.Customers;
using CBS.Domain.Customers.ValueObjects;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class AccountSignatoryMandateConfiguration : IEntityTypeConfiguration<AccountSignatoryMandate>
{
    public void Configure(EntityTypeBuilder<AccountSignatoryMandate> builder)
    {
        builder.ToTable("account_signatory_mandates");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("mandate_id")
            .IsRequired();

        builder.Property(m => m.AccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.HasIndex(m => m.AccountNumber);

        builder.Property(m => m.OperationalRule)
            .HasConversion<string>()
            .HasColumnName("operational_rule")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(m => m.SingleSignatoryThresholdBdt)
            .HasColumnName("single_signatory_threshold_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(m => m.Signatories)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<MandateSignatoryRule>>(v, (JsonSerializerOptions?)null) ?? new List<MandateSignatoryRule>())
            .HasColumnName("signatories_json")
            .IsRequired();

        builder.Property(m => m.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(m => m.EffectiveFromUtc)
            .HasColumnName("effective_from_utc")
            .IsRequired();

        builder.Property(m => m.ApprovedByUserId)
            .HasColumnName("approved_by_user_id")
            .HasMaxLength(50)
            .IsRequired();
    }
}
