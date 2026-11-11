using CBS.Domain.Enums;

namespace CBS.Application.Customers.DTOs;

public record RegisterSpecimenSignatureRequest(
    string CustomerCif,
    string SignatoryName,
    string Designation,
    string NationalIdOrPassport,
    SignatureType Type,
    string RawSignatureDataOrBase64,
    string CreatedByUserId,
    string? WorkstationIp);

public record SpecimenSignatureResponse(
    Guid Id,
    string CustomerCif,
    string SignatoryName,
    string Designation,
    string NationalIdOrPassport,
    string Type,
    string IntegrityChecksumSha256,
    string Status,
    string CreatedByUserId,
    string WorkstationIp,
    DateTime CreatedAtUtc);

public record MandateSignatoryRuleDto(
    Guid SignatoryId,
    string SignatoryName,
    StakeholderRole Role,
    bool IsMandatorySignatory);

public record DefineAccountMandateRequest(
    string AccountNumber,
    MandateOperationalRule OperationalRule,
    decimal SingleSignatoryThresholdBdt,
    List<MandateSignatoryRuleDto> Signatories,
    string ApprovedByUserId);

public record ResolveMandateRequest(
    string AccountNumber,
    List<Guid> PresentingSignatoryIds,
    decimal TransactionAmountBdt);

public record MandateResolutionResponse(
    string AccountNumber,
    bool IsAuthorized,
    string OperationalRule,
    string ResolutionMessage);

public record CreateCorporateCustomerRequest(
    string CifNumber,
    string TradeName,
    CorporateLegalForm LegalForm,
    string RegistrationNumber,
    DateOnly IncorporationDate,
    string TaxIdentificationNumber,
    string SectorCode,
    decimal AuthorizedCapitalBdt,
    decimal PaidUpCapitalBdt,
    decimal AnnualTurnoverBdt,
    int EmployeeCount,
    string? CorporateGroupId,
    string MakerUserId);

public record AddCorporateStakeholderRequest(
    string FullName,
    StakeholderRole Role,
    string NationalIdOrPassport,
    string? TaxId,
    decimal ShareholdingPercentage,
    bool IsPep,
    SanctionStatus? SanctionStatus);

public record CorporateStakeholderDto(
    Guid StakeholderId,
    string FullName,
    string Role,
    string NationalIdOrPassport,
    string? TaxId,
    decimal ShareholdingPercentage,
    bool IsPep,
    string SanctionStatus,
    bool IsSignificantBeneficialOwner);

public record CorporateCustomerResponse(
    string CifNumber,
    string TradeName,
    string LegalForm,
    string RegistrationNumber,
    DateOnly IncorporationDate,
    string TaxIdentificationNumber,
    string SectorCode,
    decimal AuthorizedCapitalBdt,
    decimal PaidUpCapitalBdt,
    decimal AnnualTurnoverBdt,
    int EmployeeCount,
    string? CorporateGroupId,
    string Status,
    List<CorporateStakeholderDto> Stakeholders);

public record ActivateCorporateCustomerRequest(string CheckerUserId);

public record CreateCorporateGroupRequest(
    string GroupId,
    string GroupName,
    decimal StatutoryGroupLimitBdt,
    string CreatedByUserId);

public record LinkGroupMemberRequest(
    string MemberCif);

public record CorporateGroupExposureResponse(
    string GroupId,
    string GroupName,
    decimal StatutoryGroupLimitBdt,
    List<string> MemberCifs,
    decimal TotalFundedLoansBdt,
    decimal TotalNonFundedTradeBdt,
    decimal TotalAggregateExposureBdt,
    decimal RemainingGroupLimitBdt,
    bool IsLimitBreached);

public record EvaluateEkycRiskRequest(
    string CustomerCif,
    int OnboardingChannelScore,
    int GeographicRiskScore,
    int PepRiskScore,
    int ProfessionSectorRiskScore,
    int ProductRiskScore,
    int TransactionalVolumeScore,
    int AdverseHistoryScore,
    int EntityLegalFormScore);

public record EkycRiskResponse(
    string CustomerCif,
    int CompositeScore,
    string RiskLevel,
    bool IsEddRequired);

public record RegisterCustomerDocumentRequest(
    string CustomerCif,
    CrmDocumentCategory Category,
    string DocumentNumber,
    DateOnly IssueDate,
    DateOnly? ExpiryDate,
    string IssuingAuthority);

public record CustomerDocumentResponse(
    Guid Id,
    string CustomerCif,
    string Category,
    string DocumentNumber,
    DateOnly IssueDate,
    DateOnly? ExpiryDate,
    string IssuingAuthority,
    bool IsVerified,
    string ExpiryStatus);
