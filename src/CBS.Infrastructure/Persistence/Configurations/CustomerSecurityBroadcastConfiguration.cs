using CBS.Domain.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class CustomerSecurityBroadcastConfiguration : IEntityTypeConfiguration<CustomerSecurityBroadcast>
{
    public void Configure(EntityTypeBuilder<CustomerSecurityBroadcast> builder)
    {
        builder.ToTable("customer_security_broadcasts");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("broadcast_id")
            .IsRequired();

        builder.Property(b => b.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(b => b.MessageEn)
            .HasColumnName("message_en")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(b => b.MessageBn)
            .HasColumnName("message_bn")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(b => b.Category)
            .HasColumnName("category")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.Channel)
            .HasConversion<string>()
            .HasColumnName("channel")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(b => b.EffectiveStartDate)
            .HasColumnName("effective_start_date")
            .IsRequired();

        builder.Property(b => b.EffectiveEndDate)
            .HasColumnName("effective_end_date")
            .IsRequired();

        builder.Property(b => b.ImpressionCount)
            .HasColumnName("impression_count")
            .IsRequired();

        builder.Property(b => b.PublishedByUserId)
            .HasColumnName("published_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(b => new { b.IsActive, b.Channel })
            .HasDatabaseName("ix_customer_broadcasts_active_channel");
    }
}
