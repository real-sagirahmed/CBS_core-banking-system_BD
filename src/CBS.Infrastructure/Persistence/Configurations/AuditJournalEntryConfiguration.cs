using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class AuditJournalEntryConfiguration : IEntityTypeConfiguration<AuditJournalEntry>
{
    public void Configure(EntityTypeBuilder<AuditJournalEntry> builder)
    {
        builder.ToTable("audit_journal_entries");

        builder.HasKey(e => e.SequenceNumber);

        builder.Property(e => e.SequenceNumber)
            .HasColumnName("sequence_number")
            .ValueGeneratedNever();

        builder.Property(e => e.TimestampUtc)
            .HasConversion(
                v => v.ToUniversalTime().Ticks,
                v => new DateTime(v, DateTimeKind.Utc))
            .HasColumnName("timestamp_utc")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Role)
            .HasConversion<string>()
            .HasColumnName("role")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.EventCategory)
            .HasConversion<string>()
            .HasColumnName("event_category")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Action)
            .HasColumnName("action")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.TargetEntityName)
            .HasColumnName("target_entity_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.TargetEntityId)
            .HasColumnName("target_entity_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.BeforeStateJson)
            .HasColumnName("before_state_json");

        builder.Property(e => e.AfterStateJson)
            .HasColumnName("after_state_json");

        builder.Property(e => e.TerminalIp)
            .HasColumnName("terminal_ip")
            .HasMaxLength(45)
            .IsRequired();

        builder.Property(e => e.PreviousEntryHash)
            .HasColumnName("previous_entry_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.CurrentEntryHash)
            .HasColumnName("current_entry_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(e => e.CurrentEntryHash);
        builder.HasIndex(e => e.TimestampUtc);
    }
}

