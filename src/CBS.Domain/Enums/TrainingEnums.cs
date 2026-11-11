namespace CBS.Domain.Enums;

public enum SimulationScenarioType
{
    CashCounterBalancing = 1,
    HighValueChequeReturn = 2,
    LoanDisbursementAgainstLien = 3,
    ImportLcIssuanceAndMargin = 4,
    TbmlAnomalyResolution = 5,
    UnclaimedDeposit10YearTransfer = 6,
    EodBatchCutoffFailureHandling = 7
}

public enum SimulationSessionStatus
{
    InProgress = 1,
    Passed = 2,
    Failed = 3,
    Abandoned = 4
}

public enum TrainingCurriculumType
{
    CyberSecurityAndPhishingDefense = 1,
    DoaMatrixAndDualControlGovernance = 2,
    TbmlAndSanctionScreening = 3,
    BfiuAmlCftReporting = 4,
    GeneralBankingCashAndTellerSecurity = 5,
    IslamicShariahFinancingInvariants = 6
}

public enum CertificationStatus
{
    Active = 1,
    ExpiringSoon = 2, // <= 30 days
    Lapsed = 3
}

public enum SecurityBroadcastChannel
{
    AtmScreenBanner = 1,
    MobileAppNotification = 2,
    InternetBankingAlert = 3,
    SmsNotice = 4
}

public enum BbrpdModuleId
{
    Module01_GeneralBanking = 1,
    Module02_BillsAndRemittance = 2,
    Module03_Deposits = 3,
    Module04_LoansAndAdvances = 4,
    Module05_TradeService = 5,
    Module06_Treasury = 6,
    Module07_Adc = 7,
    Module08_Crm = 8,
    Module09_SecurityAndCompliance = 9,
    Module10_SystemAdmin = 10,
    Module11_Reporting = 11,
    Module12_UserTrainingAndAwareness = 12
}
