using CBS.Domain.Enums;

namespace CBS.Domain.Customers.ValueObjects;

// ARCHITECTURAL INTENT: Defines the authorized signatory role within an account operational mandate (BRPD 8.1.12-d).
public sealed record MandateSignatoryRule
{
    public Guid SignatoryId { get; init; }
    public string SignatoryName { get; init; }
    public StakeholderRole Role { get; init; }
    public bool IsMandatorySignatory { get; init; }

    #pragma warning disable CS8618
    public MandateSignatoryRule() { }
    #pragma warning restore CS8618

    [System.Text.Json.Serialization.JsonConstructor]
    public MandateSignatoryRule(
        Guid signatoryId,
        string signatoryName,
        StakeholderRole role,
        bool isMandatorySignatory)
    {
        if (signatoryId == Guid.Empty)
            throw new ArgumentException("Signatory ID cannot be empty.", nameof(signatoryId));
        if (string.IsNullOrWhiteSpace(signatoryName))
            throw new ArgumentException("Signatory name is required.", nameof(signatoryName));

        SignatoryId = signatoryId;
        SignatoryName = signatoryName.Trim();
        Role = role;
        IsMandatorySignatory = isMandatorySignatory;
    }
}
