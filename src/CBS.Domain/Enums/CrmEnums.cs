namespace CBS.Domain.Enums;

public enum MandateOperationalRule
{
    Singly = 1,
    EitherOrSurvivor = 2,
    JointlyAll = 3,
    JointlyAnyTwo = 4,
    ManagingDirectorWithAnyDirector = 5
}

public enum SignatureType
{
    Signature = 1,
    Thumbprint = 2,
    BiometricStamp = 3
}

public enum SignatureStatus
{
    Active = 1,
    Revoked = 2,
    Expired = 3
}

public enum CorporateLegalForm
{
    Proprietorship = 1,
    Partnership = 2,
    PrivateLimited = 3,
    PublicLimited = 4,
    TrustOrSociety = 5
}

public enum StakeholderRole
{
    Chairman = 1,
    ManagingDirector = 2,
    Director = 3,
    Shareholder = 4,
    BeneficialOwner = 5,
    Signatory = 6
}

public enum CrmDocumentCategory
{
    SmartNid = 1,
    Passport = 2,
    TradeLicense = 3,
    TinCertificate = 4,
    IncorporationCertificate = 5,
    MemorandumArticles = 6,
    BoardResolution = 7,
    WorkPermit = 8,
    TaxReturnAcknowledgementReceipt = 9
}

public enum DocumentExpiryStatus
{
    Valid = 1,
    ExpiringSoon = 2,
    Expired = 3
}
