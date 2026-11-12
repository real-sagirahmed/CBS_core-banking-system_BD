namespace CBS.Domain.Enums;

public enum CustomerType
{
    Individual = 1,
    Joint = 2,
    Minor = 3,
    Staff = 4,
    Proprietorship = 5,
    Partnership = 6,
    PrivateLimited = 7,
    PublicLimited = 8,
    Government = 9,
    Autonomous = 10,
    NonProfitOrganization = 11,
    NonResident = 12,
    Trustee = 13
}

public enum CustomerRiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum PepStatus
{
    None = 0,
    PoliticallyExposedPerson = 1,
    FamilyOrCloseAssociateOfPep = 2,
    HighOfficialInternationalOrg = 3,
    InfluentialPerson = 4
}

public enum IdentificationDocumentType
{
    SmartNid = 1,
    LegacyNid = 2,
    Passport = 3,
    BirthCertificate = 4,
    DrivingLicense = 5,
    TradeLicense = 6,
    CertificateOfIncorporation = 7
}

public enum FatcaClassification
{
    NonUsPerson = 0,
    UsCitizen = 1,
    UsGreenCardHolder = 2,
    UsResident = 3,
    UsEntity = 4,
    UsPersonCompliant = 5,
    RecalcitrantAccountHolder = 6,
    ExemptEntity = 7
}

public enum SanctionStatus
{
    Clean = 1,
    PotentialMatch = 2,
    ConfirmedSanctionHit = 3
}

public enum CustomerLifecycleStatus
{
    Draft = 1,
    PendingCheckerApproval = 2,
    Active = 3,
    Suspended = 4,
    Closed = 5,
    Rejected = 6
}
