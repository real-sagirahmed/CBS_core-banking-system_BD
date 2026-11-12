using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class BreakGlassEmergencySessionConfiguration : IEntityTypeConfiguration<BreakGlassEmergencySession>
{
    public void Configure(EntityTypeBuilder<BreakGlassEmergencySession> builder)
    {
        builder.ToTable("break_glass_emergency_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("session_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.RequestorUserId)
            .HasColumnName("requestor_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.TargetRole)
            .HasColumnName("target_role")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.EmergencyJustification)
            .HasColumnName("emergency_justification")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.FirstApproverUserId)
            .HasColumnName("first_approver_user_id")
            .HasMaxLength(50);

        builder.Property(s => s.FirstApprovedAtUtc)
            .HasColumnName("first_approved_at_utc");

        builder.Property(s => s.SecondApproverUserId)
            .HasColumnName("second_approver_user_id")
            .HasMaxLength(50);

        builder.Property(s => s.SecondApprovedAtUtc)
            .HasColumnName("second_approved_at_utc");

        builder.Property(s => s.SessionExpiresAtUtc)
            .HasColumnName("session_expires_at_utc");

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(s => s.RevokedByUserId)
            .HasColumnName("revoked_by_user_id")
            .HasMaxLength(50);

        builder.Property(s => s.RevocationReason)
            .HasColumnName("revocation_reason")
            .HasMaxLength(500);

        builder.Property(s => s.RevokedAtUtc)
            .HasColumnName("revoked_at_utc");

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();
    }
}
