using System.Text.Json;
using CBS.Domain.BranchCash;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TellerDrawerConfiguration : IEntityTypeConfiguration<TellerDrawer>
{
    public void Configure(EntityTypeBuilder<TellerDrawer> builder)
    {
        builder.ToTable("teller_drawers");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id");

        builder.Property(d => d.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(d => d.CashierUserId)
            .HasColumnName("cashier_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.OperatingDate)
            .HasColumnName("operating_date")
            .IsRequired();

        builder.Property(d => d.CashierPostingLimit)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("cashier_posting_limit_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.CurrentBalance)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("current_balance_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.Denominations)
            .HasConversion(
                v => JsonSerializer.Serialize(v.Counts, (JsonSerializerOptions?)null),
                v => new CashDenominationBundle(JsonSerializer.Deserialize<Dictionary<int, int>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<int, int>()))
            .HasColumnName("denominations_json")
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasColumnName("drawer_status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(d => d.OpeningSupervisorUserId)
            .HasColumnName("opening_supervisor_user_id")
            .HasMaxLength(50);

        builder.Property(d => d.ClosingManagerUserId)
            .HasColumnName("closing_manager_user_id")
            .HasMaxLength(50);

        builder.Property(d => d.DiscrepancyAmount)
            .HasColumnName("discrepancy_amount_bdt")
            .HasPrecision(18, 2);

        builder.Ignore(d => d.DomainEvents);

        builder.HasIndex(d => new { d.CashierUserId, d.OperatingDate }).HasDatabaseName("ix_teller_drawers_cashier_date");
        builder.HasIndex(d => d.BranchCode).HasDatabaseName("ix_teller_drawers_branch");
    }
}
