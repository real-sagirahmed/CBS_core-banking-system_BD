namespace CBS.Domain.Enums;

public enum LoanFacilityType
{
    ContinuousLoan_CC = 1,
    ContinuousLoan_OD = 2,
    TermLoan = 3,
    DemandLoan = 4,
    Islamic_BaiMurabaha = 5,
    Islamic_Quard = 6
}

public enum LoanRepaymentFrequency
{
    Monthly = 1,
    Quarterly = 2,
    HalfYearly = 3,
    LumpSumAtMaturity = 4
}

public enum LoanClassificationGrade
{
    Standard = 1,                   // DPD < 60 days
    SpecialMentionAccount_SMA = 2,  // 60 <= DPD < 180 days
    SubStandard_SS = 3,             // 180 <= DPD < 270 days (Interest suspended)
    Doubtful_DF = 4,                // 270 <= DPD < 360 days (Interest suspended)
    BadLoss_BL = 5                  // DPD >= 360 days (Interest suspended, 100% provision)
}

public enum CollateralType
{
    LienOnDeposit_FDR = 1,
    MortgageOnRealEstate = 2,
    HypothecationOfGoods = 3,
    CorporateGuarantee = 4
}

public enum RescheduleApprovalAuthority
{
    BranchManager = 1,
    ManagingDirector = 2,
    ExecutiveCommittee = 3,
    BoardOfDirectors = 4,
    CentralBankApproval = 5
}

public enum RescheduleStatus
{
    Requested = 1,
    Approved = 2,
    Rejected = 3,
    Active = 4,
    Defaulted = 5
}

public enum WriteOffStatus
{
    Proposed = 1,
    ApprovedByBoard = 2,
    WrittenOff = 3,
    PartiallyRecovered = 4,
    FullyRecovered = 5
}

public enum LegalCaseType
{
    ArthaRinAdalatAin2003 = 1,
    BankruptcyAct1997 = 2,
    NegotiableInstrumentsAct138 = 3,
    ExemptedUnder500k = 4
}

public enum QualitativeJudgmentReason
{
    FraudOrForgery = 1,
    BusinessSuspensionOrClosure = 2,
    BorrowerDeceasedNoGuarantor = 3,
    LitigationAdverseCourtOrder = 4,
    ProjectAbandonment = 5,
    CentralBankInspectionFinding = 6
}

public enum LoanAccountStatus
{
    Active = 1,
    Matured = 2,
    Rescheduled = 3,
    WrittenOff = 4,
    Closed = 5
}
