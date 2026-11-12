using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class UserActiveSessionConfiguration : IEntityTypeConfiguration<UserActiveSession>
{
    public void Configure(EntityTypeBuilder<UserActiveSession> builder)
    {
        builder.ToTable("user_active_sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("session_id").HasMaxLength(64);

        builder.Property(s => s.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(s => s.TerminalIp).HasColumnName("terminal_ip").HasMaxLength(64).IsRequired();
        builder.Property(s => s.AppServerIp).HasColumnName("app_server_ip").HasMaxLength(64).IsRequired();
        builder.Property(s => s.BranchCode).HasColumnName("branch_code").HasMaxLength(32).IsRequired();
        builder.Property(s => s.LoginTimeUtc).HasColumnName("login_time_utc").IsRequired();
        builder.Property(s => s.LastActivityUtc).HasColumnName("last_activity_utc").IsRequired();
        builder.Property(s => s.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(s => s.TerminationReason).HasColumnName("termination_reason").HasConversion<string>().HasMaxLength(64);
        builder.Property(s => s.TerminationNotes).HasColumnName("termination_notes").HasMaxLength(500);
        builder.Property(s => s.TerminatedAtUtc).HasColumnName("terminated_at_utc");

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.IsActive);
        builder.HasIndex(s => s.LoginTimeUtc);
    }
}
