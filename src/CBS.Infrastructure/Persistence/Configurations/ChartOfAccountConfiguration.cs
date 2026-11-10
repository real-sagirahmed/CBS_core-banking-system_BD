using CBS.Domain.GeneralLedger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ChartOfAccountConfiguration : IEntityTypeConfiguration<ChartOfAccount>
{
    public void Configure(EntityTypeBuilder<ChartOfAccount> builder)
    {
        builder.ToTable("chart_of_accounts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.Category)
            .HasConversion<string>()
            .HasColumnName("category")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.ParentCode)
            .HasColumnName("parent_code")
            .HasMaxLength(20);

        builder.Property(c => c.Level)
            .HasColumnName("level")
            .IsRequired();

        builder.Property(c => c.IsLeaf)
            .HasColumnName("is_leaf")
            .IsRequired();

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(c => c.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(5)
            .IsRequired();

        builder.HasIndex(c => c.Category).HasDatabaseName("ix_chart_of_accounts_category");
        builder.HasIndex(c => c.ParentCode).HasDatabaseName("ix_chart_of_accounts_parent_code");
    }
}
