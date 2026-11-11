namespace CBS.Domain.Enums;

public enum ShariahContractType
{
    AlWadiahCurrent = 1,
    MudarabaSavings = 2,
    MudarabaTermDeposit = 3,
    MudarabaSpecialNotice = 4,
    BaiMurabaha = 5,
    Quard = 6,
    Ijara = 7,
    Musharaka = 8
}

public enum MudarabaPoolType
{
    GeneralInvestmentPool = 1,
    SpecialInvestmentPool = 2,
    TreasurySukukPool = 3
}

public enum ShariahFacilityStatus
{
    Draft = 1,
    GoodsInspected = 2,
    DisbursedToVendor = 3,
    Matured = 4,
    Overdue = 5,
    Settled = 6
}

public enum TaWidhStatus
{
    AccruedInSuspense = 1,
    ApprovedByShariahBoard = 2,
    DisbursedToCharity = 3
}
