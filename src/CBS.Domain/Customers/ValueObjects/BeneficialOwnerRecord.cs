using System;
using System.Text.Json.Serialization;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Customers.ValueObjects;

// ARCHITECTURAL INTENT: BFIU / AML Ultimate Beneficial Owner (UBO) Transparency Lock (BRPD 8.1.12 & BFIU Circular 26).
// Natural persons with >= 25% voting rights, capital interest, or effective controlling mind must be identified.

public sealed record BeneficialOwnerRecord
{
    public Guid OwnerId { get; init; }
    public string FullName { get; init; }
    public string NationalId { get; init; }
    public BeneficialOwnerType OwnerType { get; init; }
    public decimal ShareholdingPercentage { get; init; }
    public bool IsPepOrIp { get; init; }
    public string VerificationStatus { get; init; }
    public DateTime RegisteredAtUtc { get; init; }

    #pragma warning disable CS8618
    public BeneficialOwnerRecord() { }
    #pragma warning restore CS8618

    [JsonConstructor]
    public BeneficialOwnerRecord(
        Guid ownerId,
        string fullName,
        string nationalId,
        BeneficialOwnerType ownerType,
        decimal shareholdingPercentage,
        bool isPepOrIp,
        string verificationStatus,
        DateTime registeredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Beneficial owner full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(nationalId))
            throw new ArgumentException("Beneficial owner National ID (NID) is required.", nameof(nationalId));

        if (ownerType == BeneficialOwnerType.NaturalPersonMajorityShareholder && shareholdingPercentage < 25.00m)
        {
            throw new InvariantViolationException(
                "UboShareholdingThresholdInvariant",
                "BRPD Circular 12/2024 (8.1.12) & BFIU Master Circular 26",
                $"Majority shareholder UBO must hold at least 25% shareholding. Provided: {shareholdingPercentage}%.");
        }

        if (shareholdingPercentage < 0 || shareholdingPercentage > 100)
            throw new ArgumentException("Shareholding percentage must be between 0 and 100.", nameof(shareholdingPercentage));

        OwnerId = ownerId == Guid.Empty ? Guid.NewGuid() : ownerId;
        FullName = fullName.Trim();
        NationalId = nationalId.Trim().ToUpperInvariant();
        OwnerType = ownerType;
        ShareholdingPercentage = shareholdingPercentage;
        IsPepOrIp = isPepOrIp;
        VerificationStatus = string.IsNullOrWhiteSpace(verificationStatus) ? "Verified" : verificationStatus.Trim();
        RegisteredAtUtc = registeredAtUtc == default ? DateTime.UtcNow : registeredAtUtc;
    }
}
