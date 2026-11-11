using System.Text.Json;
using CBS.Domain.BranchCash;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class BranchVaultConfiguration : IEntityTypeConfiguration<BranchVault>
{
    public void Configure(EntityTypeBuilder<BranchVault> builder)
    {
        builder.ToTable("branch_vaults");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("id");

        builder.Property(v => v.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(v => v.VaultCustodianUserId)
            .HasColumnName("vault_custodian_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(v => v.JointCustodianUserId)
            .HasColumnName("joint_custodian_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(v => v.CurrentBalance)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("current_balance_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(v => v.InsuredCashLimit)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("insured_cash_limit_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(v => v.Denominations)
            .HasConversion(
                v => JsonSerializer.Serialize(v.Counts, (JsonSerializerOptions?)null),
                v => new CashDenominationBundle(JsonSerializer.Deserialize<Dictionary<int, int>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<int, int>()))
            .HasColumnName("denominations_json")
            .IsRequired();

        builder.Property(v => v.IsVaultLocked)
            .HasColumnName("is_vault_locked")
            .IsRequired();

        builder.Property(v => v.EveningBankingSessionActive)
            .HasColumnName("evening_banking_session_active")
            .IsRequired();

        builder.Property(v => v.EveningSessionOpenedAtUtc)
            .HasColumnName("evening_session_opened_at_utc");

        builder.Property(v => v.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(v => v.LastModifiedAtUtc)
            .HasColumnName("last_modified_at_utc");

        builder.Ignore(v => v.DomainEvents);

        builder.HasIndex(v => v.BranchCode).IsUnique().HasDatabaseName("ix_branch_vaults_branch_code");
    }
}
