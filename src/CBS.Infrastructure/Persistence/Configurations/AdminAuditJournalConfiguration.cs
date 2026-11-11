using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class AdminAuditJournalConfiguration : IEntityTypeConfiguration<AdminAuditJournal>
{
    public void Configure(EntityTypeBuilder<AdminAuditJournal> builder)
    {
        builder.ToTable("admin_audit_journals");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .HasColumnName("journal_id")
            .IsRequired();

        builder.Property(j => j.Category)
            .HasConversion<string>()
            .HasColumnName("category")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.Action)
            .HasColumnName("action")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(j => j.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(j => j.ActorUserId)
            .HasColumnName("actor_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.WorkstationIp)
            .HasColumnName("workstation_ip")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.TimestampUtc)
            .HasColumnName("timestamp_utc")
            .IsRequired();

        builder.Property(j => j.OldValueJson)
            .HasColumnName("old_value_json");

        builder.Property(j => j.NewValueJson)
            .HasColumnName("new_value_json");

        builder.Property(j => j.Remarks)
            .HasColumnName("remarks")
            .HasMaxLength(1000);
    }
}
