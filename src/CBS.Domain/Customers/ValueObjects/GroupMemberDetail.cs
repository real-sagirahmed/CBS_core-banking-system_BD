using System;
using System.Text.Json.Serialization;
using CBS.Domain.Enums;

namespace CBS.Domain.Customers.ValueObjects;

// ARCHITECTURAL INTENT: Corporate Conglomerate Member Entity Mapping (BRPD 8.1.1-c, 8.1.7).
// Represents child/sister concerns, subsidiaries, and key guarantors associated with a Corporate Group ID.

public sealed record GroupMemberDetail
{
    public string CifNumber { get; init; }
    public string EntityName { get; init; }
    public CustomerGroupRole Role { get; init; }
    public decimal ShareholdingPercentage { get; init; }
    public string RegistrationNumber { get; init; }
    public DateTime LinkedAtUtc { get; init; }

    #pragma warning disable CS8618
    public GroupMemberDetail() { }
    #pragma warning restore CS8618

    [JsonConstructor]
    public GroupMemberDetail(
        string cifNumber,
        string entityName,
        CustomerGroupRole role,
        decimal shareholdingPercentage,
        string registrationNumber,
        DateTime linkedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(cifNumber))
            throw new ArgumentException("Member CIF number is required.", nameof(cifNumber));
        if (string.IsNullOrWhiteSpace(entityName))
            throw new ArgumentException("Entity trade name is required.", nameof(entityName));

        CifNumber = cifNumber.Trim().ToUpperInvariant();
        EntityName = entityName.Trim();
        Role = role;
        ShareholdingPercentage = shareholdingPercentage;
        RegistrationNumber = registrationNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        LinkedAtUtc = linkedAtUtc == default ? DateTime.UtcNow : linkedAtUtc;
    }
}
