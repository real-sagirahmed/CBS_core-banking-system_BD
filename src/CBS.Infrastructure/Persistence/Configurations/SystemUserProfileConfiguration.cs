using System.Text.Json;
using CBS.Domain.Security;
using CBS.Domain.Security.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class SystemUserProfileConfiguration : IEntityTypeConfiguration<SystemUserProfile>
{
    public void Configure(EntityTypeBuilder<SystemUserProfile> builder)
    {
        builder.ToTable("system_user_profiles");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.EmployeeId)
            .HasColumnName("employee_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(u => u.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(u => u.UserGroup)
            .HasConversion<string>()
            .HasColumnName("user_group")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(u => u.AssignedRoles)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasColumnName("assigned_roles_json")
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(u => u.MustChangePasswordOnNextLogin)
            .HasColumnName("must_change_password_on_next_login")
            .IsRequired();

        builder.Property(u => u.PasswordLastChangedAtUtc)
            .HasColumnName("password_last_changed_at_utc")
            .IsRequired();

        builder.Property(u => u.PasswordExpiryDays)
            .HasColumnName("password_expiry_days")
            .IsRequired();

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(u => u.ActiveSessionToken)
            .HasColumnName("active_session_token")
            .HasMaxLength(100);

        builder.Property(u => u.LastLoginWorkstationIp)
            .HasColumnName("last_login_workstation_ip")
            .HasMaxLength(50);

        builder.Property(u => u.LastLoginAtUtc)
            .HasColumnName("last_login_at_utc");

        builder.Property(u => u.PreviousSuccessfulLoginAtUtc)
            .HasColumnName("previous_successful_login_at_utc");

        builder.Property(u => u.PreviousSuccessfulLoginIp)
            .HasColumnName("previous_successful_login_ip")
            .HasMaxLength(50);

        builder.Property(u => u.PreviousFailedLoginAtUtc)
            .HasColumnName("previous_failed_login_at_utc");

        builder.Property(u => u.PreviousFailedLoginIp)
            .HasColumnName("previous_failed_login_ip")
            .HasMaxLength(50);

        builder.Property(u => u.ConsecutiveFailedLoginAttempts)
            .HasColumnName("consecutive_failed_login_attempts")
            .IsRequired();

        builder.Property(u => u.PasswordHistory)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<PasswordHistoryEntry>>(v, (JsonSerializerOptions?)null) ?? new List<PasswordHistoryEntry>())
            .HasColumnName("password_history_json")
            .IsRequired();

        builder.Property(u => u.DoaLimits)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<DoaLimitProfile>(v, (JsonSerializerOptions?)null) ?? new DoaLimitProfile())
            .HasColumnName("doa_limits_json")
            .IsRequired();

        builder.Property(u => u.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(u => u.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.ApprovedByUserId)
            .HasColumnName("approved_by_user_id")
            .HasMaxLength(50);

        builder.Property(u => u.ApprovedAtUtc)
            .HasColumnName("approved_at_utc");
    }
}
