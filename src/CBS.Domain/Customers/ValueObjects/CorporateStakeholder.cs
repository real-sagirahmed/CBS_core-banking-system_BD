using CBS.Domain.Enums;

namespace CBS.Domain.Customers.ValueObjects;

// ARCHITECTURAL INTENT: Stakeholders and Ultimate Beneficial Owners (UBOs)
// must be explicitly identified, including equity percentage, PEP status, and sanction screening (BRPD 8.1.10, 8.1.18-i).
public sealed record CorporateStakeholder
{
    public Guid StakeholderId { get; init; }
    public string FullName { get; init; }
    public StakeholderRole Role { get; init; }
    public string NationalIdOrPassport { get; init; }
    public string? TaxId { get; init; }
    public decimal ShareholdingPercentage { get; init; }
    public bool IsPep { get; init; }
    public SanctionStatus SanctionStatus { get; init; }

    #pragma warning disable CS8618
    public CorporateStakeholder() { }
    #pragma warning restore CS8618

    [System.Text.Json.Serialization.JsonConstructor]
    public CorporateStakeholder(
        Guid stakeholderId,
        string fullName,
        StakeholderRole role,
        string nationalIdOrPassport,
        string? taxId,
        decimal shareholdingPercentage,
        bool isPep,
        SanctionStatus sanctionStatus)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Stakeholder full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(nationalIdOrPassport))
            throw new ArgumentException("Stakeholder NID or Passport is required.", nameof(nationalIdOrPassport));
        if (shareholdingPercentage < 0 || shareholdingPercentage > 100)
            throw new ArgumentException("Shareholding percentage must be between 0 and 100.", nameof(shareholdingPercentage));

        StakeholderId = stakeholderId == Guid.Empty ? Guid.NewGuid() : stakeholderId;
        FullName = fullName.Trim();
        Role = role;
        NationalIdOrPassport = nationalIdOrPassport.Trim().ToUpperInvariant();
        TaxId = taxId?.Trim().ToUpperInvariant();
        ShareholdingPercentage = shareholdingPercentage;
        IsPep = isPep;
        SanctionStatus = sanctionStatus;
    }

    public bool IsSignificantBeneficialOwner => ShareholdingPercentage >= 10.00m;
}
