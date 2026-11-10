namespace CBS.Domain.Enums;

public enum ForeignRemittanceDirection
{
    Inward = 1,
    Outward = 2
}

public enum ForeignRemittanceType
{
    WageEarnerInward = 1,
    ExportProceedsInward = 2,
    AdvanceTtInward = 3,
    GeneralCommercialInward = 4,
    StudentFileOutward = 5,
    MedicalOutward = 6,
    SoftwareServiceOutward = 7,
    AdvanceTtOutward = 8,
    GeneralCommercialOutward = 9
}

public enum ForeignRemittanceStatus
{
    Draft = 1,
    PendingSanctionScreening = 2,
    HeldForSanctionReview = 3,
    PendingCheckerApproval = 4,
    ApprovedPendingSettlement = 5,
    CreditedToBeneficiary = 6,
    SwiftTransmitted = 7,
    Cancelled = 8,
    Rejected = 9
}

public enum RemittanceChargeParty
{
    OUR = 1,
    BEN = 2,
    SHA = 3
}

public enum GovernmentIncentiveStatus
{
    NotApplicable = 0,
    Calculated = 1,
    DisbursedToBeneficiary = 2,
    ClaimBatched = 3,
    ReimbursedByCentralBank = 4
}

public enum IncentiveClaimBatchStatus
{
    Draft = 1,
    Batched = 2,
    SubmittedToBangladeshBank = 3,
    Reimbursed = 4,
    Rejected = 5
}
