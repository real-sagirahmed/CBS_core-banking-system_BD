using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TbmlPriceBenchmarkConfiguration : IEntityTypeConfiguration<TbmlPriceBenchmark>
{
    public void Configure(EntityTypeBuilder<TbmlPriceBenchmark> builder)
    {
        builder.ToTable("tbml_price_benchmarks");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("benchmark_id")
            .IsRequired();

        builder.Property(b => b.HsCode)
            .HasColumnName("hs_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.ItemDescription)
            .HasColumnName("item_description")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(b => b.UnitOfMeasure)
            .HasColumnName("unit_of_measure")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.BenchmarkPriceMin)
            .HasColumnName("benchmark_price_min")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.BenchmarkPriceMax)
            .HasColumnName("benchmark_price_max")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.Currency)
            .HasConversion(v => v.Code, v => Currency.FromCode(v))
            .HasColumnName("currency_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(b => b.EffectiveDate)
            .HasColumnName("effective_date")
            .IsRequired();

        builder.Property(b => b.SourceReference)
            .HasColumnName("source_reference")
            .HasMaxLength(150)
            .IsRequired();

        builder.Ignore(b => b.DomainEvents);
    }
}
