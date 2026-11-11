using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class AdminActionProposalConfiguration : IEntityTypeConfiguration<AdminActionProposal>
{
    public void Configure(EntityTypeBuilder<AdminActionProposal> builder)
    {
        builder.ToTable("admin_action_proposals");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("proposal_id")
            .IsRequired();

        builder.Property(p => p.ActionType)
            .HasConversion<string>()
            .HasColumnName("action_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.TargetEntityId)
            .HasColumnName("target_entity_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.ProposalPayloadJson)
            .HasColumnName("proposal_payload_json")
            .IsRequired();

        builder.Property(p => p.MakerUserId)
            .HasColumnName("maker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.ProposedAtUtc)
            .HasColumnName("proposed_at_utc")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.CheckerUserId)
            .HasColumnName("checker_user_id")
            .HasMaxLength(50);

        builder.Property(p => p.ReviewedAtUtc)
            .HasColumnName("reviewed_at_utc");

        builder.Property(p => p.ReviewRemarks)
            .HasColumnName("review_remarks")
            .HasMaxLength(500);
    }
}
