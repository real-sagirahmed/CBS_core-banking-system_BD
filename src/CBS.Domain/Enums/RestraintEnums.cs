namespace CBS.Domain.Enums;

// ARCHITECTURAL INTENT: Enumerations for Account Restraints, Liens, Freezes and Bulk Processing
// strictly grounded in Bangladesh Bank BRPD Circular No. 12/2024 Modules 1.6 & 3.1.

public enum RestraintType
{
    CourtAttachmentOrder = 1,
    NbrGarnisheeOrder = 2,
    BfiuSuspiciousFreeze = 3,
    GeneralLien = 4,
    SecurityDepositLien = 5,
    LoanCollateralPledge = 6,
    InternalInvestigation = 7,
    DeceasedEstateRestraint = 8
}

public enum RestraintScope
{
    TotalFreeze = 1,      // Blocks both Debit and Credit transactions (BRPD 1.6.2-v, 3.1.15-i)
    DebitBlocked = 2,     // Blocks Debit transactions, allows Credit postings only (BRPD 1.6.2-v, 3.1.15-vi)
    CreditBlocked = 3,    // Blocks Credit transactions (BRPD 3.1.15-vii)
    PartialAmountHold = 4 // Blocks a specific BDT amount irrespective of balance (BRPD 3.1.15-ix, x)
}

public enum RestraintStatus
{
    Active = 1,
    Released = 2,
    ForeclosedRealized = 3,
    Expired = 4
}

public enum BulkBatchType
{
    SalaryDisbursement = 1,
    DividendDistribution = 2,
    RefundWarrant = 3
}

public enum BulkBatchStatus
{
    Uploaded = 1,
    Validated = 2,
    PartiallyValidated = 3,
    Executed = 4,
    CompletedWithErrors = 5,
    Rejected = 6
}

public enum BulkItemStatus
{
    Pending = 1,
    Valid = 2,
    Invalid = 3,
    Posted = 4,
    Failed = 5
}
