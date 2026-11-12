using System.Collections.Generic;
using System.Text.Json;
using CBS.Domain.Customers;
using CBS.Domain.Customers.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class CorporateGroupConfiguration : IEntityTypeConfiguration<CorporateGroup>
{
    public void Configure(EntityTypeBuilder<CorporateGroup> builder)
    {
        builder.ToTable("corporate_groups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasColumnName("group_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(g => g.GroupName)
            .HasColumnName("group_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(g => g.ParentCifNumber)
            .HasColumnName("parent_cif_number")
            .HasMaxLength(50)
            .HasDefaultValue(string.Empty);

        builder.Property(g => g.MemberCifNumbers)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasColumnName("member_cifs_json")
            .IsRequired();

        builder.Property(g => g.MemberDetails)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<GroupMemberDetail>>(v, (JsonSerializerOptions?)null) ?? new List<GroupMemberDetail>())
            .HasColumnName("member_details_json")
            .HasDefaultValueSql("'[]'");

        builder.Property(g => g.BeneficialOwners)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<BeneficialOwnerRecord>>(v, (JsonSerializerOptions?)null) ?? new List<BeneficialOwnerRecord>())
            .HasColumnName("beneficial_owners_json")
            .HasDefaultValueSql("'[]'");

        builder.Property(g => g.StatutoryGroupLimitBdt)
            .HasColumnName("statutory_group_limit_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(g => g.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(g => g.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();
    }
}
