// ARCHITECTURAL INTENT: Standardized Enums for BACPS Clearing, Cheque Dishonour Return Memo,
// NI Act 1881 Section 138 Legal Notice, Bills for Collection (OBC/IBC), and Inland Bills Purchased (IBP)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 1, 2, and 4, and BACPS Operating Rules.

namespace CBS.Domain.Enums;

public enum BacpsReturnReasonCode
{
    RR01_InsufficientFunds = 1,
    RR02_SignatureDiffers = 2,
    RR03_PostDatedCheque = 3,
    RR04_StaleCheque = 4,
    RR05_PaymentStoppedByDrawer = 5,
    RR06_WordsAndFiguresDiffer = 6,
    RR07_AccountBlockedOrFrozen = 7,
    RR08_ChequeMutilatedOrTorn = 8,
    RR09_IrregularEndorsement = 9,
    RR10_AlterationRequiresDrawerSignature = 10,
    RR11_AccountClosed = 11,
    RR12_PresentedBeyondValidity = 12
}

public enum ClearingValueType
{
    RegularValue = 1, // < 500,000 BDT
    HighValue = 2     // >= 500,000 BDT (mandates dual-control authorization)
}

public enum DishonourMemoStatus
{
    Issued = 1,
    NoticeServed = 2,
    Cured = 3,
    LegalSuitFiled = 4,
    Cancelled = 5
}

public enum NiActNoticeStatus
{
    NotIssued = 1,
    Dispatched = 2,
    Delivered = 3,
    ThirtyDayWindowExpired = 4,
    SuitInstituted = 5
}

public enum BillCollectionType
{
    OutwardCleanCollection_OBC = 1,
    InwardCleanCollection_IBC = 2
}

public enum BillCollectionStatus
{
    Lodged = 1,
    Dispatched = 2,
    PresentedToDrawee = 3,
    Realized = 4,
    ReturnedDishonoured = 5,
    Cancelled = 6
}

public enum InlandBillPurchasedStatus
{
    Purchased = 1,
    SettledAtMaturity = 2,
    DishonouredAtMaturity = 3,
    CrystallizedToForcedLoan = 4
}
