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
