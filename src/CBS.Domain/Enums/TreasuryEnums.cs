namespace CBS.Domain.Enums;

public enum TreasuryDealType
{
    FxSpot = 1,
    FxForward = 2,
    FxSwap = 3,
    CallMoneyBorrowing = 4,
    CallMoneyLending = 5,
    TermPlacement = 6,
    TermBorrowing = 7,
    TreasuryBillPurchase = 8,
    TreasuryBondPurchase = 9
}

public enum TreasuryDealStatus
{
    CapturedByFrontOffice = 1,
    VerifiedByMidOffice = 2,
    SettledByBackOffice = 3,
    Cancelled = 4
}

public enum DealDirection
{
    Buy = 1,
    Sell = 2,
    Borrow = 3,
    Lend = 4
}

public enum BankingType
{
    Conventional = 1,
    Islamic = 2
}

public enum LiquidityBucket
{
    Days1To14 = 1,
    Days15To28 = 2,
    Days29To90 = 3,
    Days91To180 = 4,
    Days181To365 = 5,
    Over1Year = 6
}

public enum TreasurySecurityType
{
    TreasuryBill91D = 1,
    TreasuryBill182D = 2,
    TreasuryBill364D = 3,
    TreasuryBond5Y = 4,
    TreasuryBond10Y = 5,
    TreasuryBond15Y = 6,
    TreasuryBond20Y = 7,
    IslamicSukuk = 8
}

public enum SecurityClassification
{
    Hft = 1, // Held for Trading (daily Mark-to-Market revaluation to reserve)
    Htm = 2  // Held to Maturity (amortized cost, exempt from daily MTM)
}
