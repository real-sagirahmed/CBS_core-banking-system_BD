using CBS.Domain.SecurityAudit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("session_id")
            .HasMaxLength(100);

        builder.Property(s => s.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.Role)
            .HasConversion<string>()
            .HasColumnName("role")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(s => s.TerminalIp)
            .HasColumnName("terminal_ip")
            .HasMaxLength(45)
            .IsRequired();

        builder.Property(s => s.LoggedInAtUtc)
            .HasColumnName("logged_in_at_utc")
            .IsRequired();

        builder.Property(s => s.LoggedOutAtUtc)
            .HasColumnName("logged_out_at_utc");

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Ignore(s => s.DomainEvents);
    }
}
