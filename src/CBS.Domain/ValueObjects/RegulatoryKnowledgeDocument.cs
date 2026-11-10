namespace CBS.Domain.ValueObjects;

/// <summary>
/// Knowledge document chunk indexed in the CBS regulatory grounding vector/knowledge store.
/// </summary>
public sealed record RegulatoryKnowledgeDocument
{
    public string DocumentId { get; init; } = string.Empty;
    public string CircularNo { get; init; } = string.Empty;
    public string ModuleReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];

    // Parameterless constructor for EF Core ORM materialization
    private RegulatoryKnowledgeDocument() { }

    public RegulatoryKnowledgeDocument(
        string documentId,
        string circularNo,
        string moduleReference,
        string title,
        string content,
        IReadOnlyList<string> tags)
    {
        DocumentId = documentId;
        CircularNo = circularNo;
        ModuleReference = moduleReference;
        Title = title;
        Content = content;
        Tags = tags ?? [];
    }
}
