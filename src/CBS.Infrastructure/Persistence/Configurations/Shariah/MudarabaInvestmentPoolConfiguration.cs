using CBS.Domain.Shariah;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations.Shariah;

public class MudarabaInvestmentPoolConfiguration : IEntityTypeConfiguration<MudarabaInvestmentPool>
{
    public void Configure(EntityTypeBuilder<MudarabaInvestmentPool> builder)
    {
        builder.ToTable("mudaraba_investment_pools");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("pool_id")
            .IsRequired();

        builder.Property(p => p.PoolName)
            .HasColumnName("pool_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(p => p.PoolType)
            .HasConversion<string>()
            .HasColumnName("pool_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.TotalDepositBalance)
            .HasColumnName("total_deposit_balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.MudaribShareRatio)
            .HasColumnName("mudarib_share_ratio")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(p => p.PerRatio)
            .HasColumnName("per_ratio")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(p => p.IrrRatio)
            .HasColumnName("irr_ratio")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(p => p.WeightagesJson)
            .HasColumnName("weightages_json")
            .IsRequired();

        builder.Property(p => p.AccumulatedPerBalance)
            .HasColumnName("accumulated_per_balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.AccumulatedIrrBalance)
            .HasColumnName("accumulated_irr_balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(p => p.LastDistributionDateUtc)
            .HasColumnName("last_distribution_date_utc");
    }
}
