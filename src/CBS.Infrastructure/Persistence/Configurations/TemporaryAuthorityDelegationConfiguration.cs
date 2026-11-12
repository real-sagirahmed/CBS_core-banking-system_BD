using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TemporaryAuthorityDelegationConfiguration : IEntityTypeConfiguration<TemporaryAuthorityDelegation>
{
    public void Configure(EntityTypeBuilder<TemporaryAuthorityDelegation> builder)
    {
        builder.ToTable("temporary_authority_delegations");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("delegation_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.DelegatorUserId)
            .HasColumnName("delegator_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.DelegateeUserId)
            .HasColumnName("delegatee_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.DelegatedRole)
            .HasColumnName("delegated_role")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.EffectiveFromUtc)
            .HasColumnName("effective_from_utc")
            .IsRequired();

        builder.Property(d => d.EffectiveToUtc)
            .HasColumnName("effective_to_utc")
            .IsRequired();

        builder.Property(d => d.DelegationReason)
            .HasColumnName("delegation_reason")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(d => d.ApprovedByUserId)
            .HasColumnName("approved_by_user_id")
            .HasMaxLength(50);

        builder.Property(d => d.ApprovedAtUtc)
            .HasColumnName("approved_at_utc");

        builder.Property(d => d.RevokedByUserId)
            .HasColumnName("revoked_by_user_id")
            .HasMaxLength(50);

        builder.Property(d => d.RevocationReason)
            .HasColumnName("revocation_reason")
            .HasMaxLength(500);

        builder.Property(d => d.RevokedAtUtc)
            .HasColumnName("revoked_at_utc");

        builder.Property(d => d.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();
    }
}
