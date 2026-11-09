using CBS.Domain.Enums;

namespace CBS.Application.Customers.DTOs;

public record CreateCustomerDraftRequest(
    CustomerType Type,
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string FatherName,
    string MotherName,
    string StreetAddress,
    string ThanaOrUpazila,
    string District,
    string PostalCode,
    string MobileNumber,
    string SectorCode,
    string? NationalId = null,
    string? TaxId = null,
    string? LegalGuardianCif = null,
    PepStatus PepStatus = PepStatus.None,
    FatcaClassification FatcaStatus = FatcaClassification.NonUsPerson);

public record CustomerResponseDto(
    string CifNumber,
    string FullName,
    string CustomerType,
    string RiskLevel,
    bool IsActive,
    bool IsElectionCommissionVerified,
    string DeduplicationFingerprint,
    string? NationalId,
    string? LegalGuardianCif);
