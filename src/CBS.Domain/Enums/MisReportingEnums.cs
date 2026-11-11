namespace CBS.Domain.Enums;

public enum RegulatoryReportType
{
    DailyCleanCash = 1,
    DailyAffairs = 2,
    TpBreachAndEol = 3,
    DepositMixAndSlabs = 4,
    UnclaimedDeposit10Year = 5,
    Sbs3LoanClassification = 6,
    ForeignTradeConsolidated = 7,
    RemittanceCashIncentiveSummary = 8,
    AdcChannelReconciliation = 9,
    RationalizedInputTemplate = 10
}

public enum UnclaimedDepositStatus
{
    Identified = 1,
    NoticeIssued = 2,
    RemittedToCentralBank = 3,
    ReclaimedByCustomer = 4
}

public enum ReportExportFormat
{
    Json = 1,
    Csv = 2,
    Xml = 3
}

public enum DepositSlabRange
{
    UpTo100K = 1,
    From100KTo500K = 2,
    From500KTo1M = 3,
    From1MTo5M = 4,
    From5MTo10M = 5,
    Above10M = 6
}

public enum EconomicSectorCategory
{
    Agriculture = 1,
    Cmsme = 2,
    LargeCorporate = 3,
    TradeAndCommerce = 4,
    ConstructionAndHousing = 5,
    ConsumerCredit = 6,
    OtherServices = 7
}
