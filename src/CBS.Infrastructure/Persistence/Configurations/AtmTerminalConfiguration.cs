using CBS.Domain.Adc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class AtmTerminalConfiguration : IEntityTypeConfiguration<AtmTerminal>
{
    public void Configure(EntityTypeBuilder<AtmTerminal> builder)
    {
        builder.ToTable("atm_terminals");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("terminal_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.TerminalName)
            .HasColumnName("terminal_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(t => t.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.Location)
            .HasColumnName("location")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(t => t.CurrentCashBalance)
            .HasColumnName("current_cash_balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.MaxCapacity)
            .HasColumnName("max_capacity")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(t => t.LastReplenishedAtUtc)
            .HasConversion(v => v.ToUniversalTime().Ticks, v => new DateTime(v, DateTimeKind.Utc))
            .HasColumnName("last_replenished_ticks")
            .IsRequired();
    }
}
