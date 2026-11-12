using System.Collections.Generic;
using System.Text.Json;
using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class UserPasswordCredentialConfiguration : IEntityTypeConfiguration<UserPasswordCredential>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<UserPasswordCredential> builder)
    {
        builder.ToTable("user_password_credentials");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.Salt)
            .HasColumnName("salt")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(c => c.LastChangedAtUtc)
            .HasColumnName("last_changed_at_utc")
            .IsRequired();

        builder.Property(c => c.IsFirstLoginPendingChange)
            .HasColumnName("is_first_login_pending_change")
            .IsRequired();

        builder.Property(c => c.FailedAttemptCount)
            .HasColumnName("failed_attempt_count")
            .IsRequired();

        builder.Property(c => c.IsLockedOut)
            .HasColumnName("is_locked_out")
            .IsRequired();

        builder.Property(c => c.LockoutExpiryUtc)
            .HasColumnName("lockout_expiry_utc");

        builder.Property(c => c.PasswordHistory)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => string.IsNullOrWhiteSpace(v) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>())
            .HasColumnName("password_history")
            .IsRequired();
    }
}
