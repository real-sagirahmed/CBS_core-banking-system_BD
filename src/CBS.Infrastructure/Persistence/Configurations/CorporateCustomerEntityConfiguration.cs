using System.Text.Json;
using CBS.Domain.Customers;
using CBS.Domain.Customers.ValueObjects;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class CorporateCustomerEntityConfiguration : IEntityTypeConfiguration<CorporateCustomerEntity>
{
    public void Configure(EntityTypeBuilder<CorporateCustomerEntity> builder)
    {
        builder.ToTable("corporate_customer_entities");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("cif_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.TradeName)
            .HasColumnName("trade_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.LegalForm)
            .HasConversion<string>()
            .HasColumnName("legal_form")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(c => c.RegistrationNumber)
            .HasColumnName("registration_number")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(c => c.RegistrationNumber);

        builder.Property(c => c.IncorporationDate)
            .HasColumnName("incorporation_date")
            .IsRequired();

        builder.Property(c => c.TaxIdentificationNumber)
            .HasColumnName("tax_identification_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.SectorCode)
            .HasColumnName("sector_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.AuthorizedCapital)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("authorized_capital_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.PaidUpCapital)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("paid_up_capital_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.AnnualTurnover)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("annual_turnover_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.EmployeeCount)
            .HasColumnName("employee_count")
            .IsRequired();

        builder.Property(c => c.CorporateGroupId)
            .HasColumnName("corporate_group_id")
            .HasMaxLength(50);

        builder.Property(c => c.Stakeholders)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<CorporateStakeholder>>(v, (JsonSerializerOptions?)null) ?? new List<CorporateStakeholder>())
            .HasColumnName("stakeholders_json")
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.MakerUserId)
            .HasColumnName("maker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.CheckerUserId)
            .HasColumnName("checker_user_id")
            .HasMaxLength(50);

        builder.Property(c => c.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(c => c.ActivatedAtUtc)
            .HasColumnName("activated_at_utc");
    }
}
