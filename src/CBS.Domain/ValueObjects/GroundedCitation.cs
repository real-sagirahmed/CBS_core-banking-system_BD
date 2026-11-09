namespace CBS.Domain.ValueObjects;

/// <summary>
/// Value Object representing a grounded reference from Bangladesh Bank regulatory frameworks.
/// </summary>
public sealed record GroundedCitation(
    string CircularReference,
    string SectionTitle,
    string RelevantExcerpt,
    decimal RelevanceScore);
