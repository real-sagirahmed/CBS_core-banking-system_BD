namespace CBS.Domain.ValueObjects;

/// <summary>
/// Knowledge document chunk indexed in the CBS regulatory grounding vector/knowledge store.
/// </summary>
public sealed record RegulatoryKnowledgeDocument(
    string DocumentId,
    string CircularNo,
    string ModuleReference,
    string Title,
    string Content,
    IReadOnlyList<string> Tags);
