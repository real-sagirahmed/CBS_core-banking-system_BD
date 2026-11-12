using CBS.Domain.Security;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class UserDoaProfileConfiguration : IEntityTypeConfiguration<UserDoaProfile>
{
    public void Configure(EntityTypeBuilder<UserDoaProfile> builder)
    {
        builder.ToTable("user_doa_profiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("profile_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.RoleName)
            .HasColumnName("role_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.MaxLocalDebitLimit)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("max_local_debit_limit")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.MaxLocalCreditLimit)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("max_local_credit_limit")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.MaxInterBranchDebitLimit)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("max_inter_branch_debit_limit")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.MaxInterBranchCreditLimit)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("max_inter_branch_credit_limit")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.MaxGlAdjustmentLimit)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("max_gl_adjustment_limit")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(p => p.AssignedByUserId)
            .HasColumnName("assigned_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.AssignedAtUtc)
            .HasColumnName("assigned_at_utc")
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");
    }
}
