using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace CBS.Infrastructure.Persistence.Configurations;

public class RegulatoryKnowledgeDocumentConfiguration : IEntityTypeConfiguration<RegulatoryKnowledgeDocument>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Configure(EntityTypeBuilder<RegulatoryKnowledgeDocument> builder)
    {
        builder.ToTable("regulatory_knowledge_documents");

        builder.HasKey(d => d.DocumentId);

        builder.Property(d => d.DocumentId)
            .HasColumnName("document_id")
            .HasMaxLength(64)
            .ValueGeneratedNever();

        builder.Property(d => d.CircularNo)
            .HasColumnName("circular_no")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(d => d.ModuleReference)
            .HasColumnName("module_reference")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(d => d.Title)
            .HasColumnName("title")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(d => d.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(d => d.Tags)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>())
            .HasColumnName("tags_json")
            .IsRequired();

        builder.HasIndex(d => d.ModuleReference);
    }
}
