using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class AdminChangeRequestConfiguration : IEntityTypeConfiguration<AdminChangeRequest>
{
    public void Configure(EntityTypeBuilder<AdminChangeRequest> builder)
    {
        builder.ToTable("admin_change_requests");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("request_id").HasMaxLength(64);

        builder.Property(c => c.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(c => c.ChangeDescription).HasColumnName("change_description").HasMaxLength(500).IsRequired();
        builder.Property(c => c.ChangePayloadJson).HasColumnName("change_payload_json").HasColumnType("text").IsRequired();
        builder.Property(c => c.MakerUserId).HasColumnName("maker_user_id").HasMaxLength(64).IsRequired();
        builder.Property(c => c.MakerBranchCode).HasColumnName("maker_branch_code").HasMaxLength(32).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(c => c.CheckerUserId).HasColumnName("checker_user_id").HasMaxLength(64);
        builder.Property(c => c.CheckerRemarks).HasColumnName("checker_remarks").HasMaxLength(500);
        builder.Property(c => c.ActionedAtUtc).HasColumnName("actioned_at_utc");
        builder.Property(c => c.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);

        builder.HasIndex(c => c.MakerUserId);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.CreatedAtUtc);
    }
}
