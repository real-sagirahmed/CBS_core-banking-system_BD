namespace CBS.Domain.Enums;

public enum CropSeasonType
{
    Aman = 1,
    Boro = 2,
    Aus = 3,
    Jute = 4,
    PotatoWheat = 5,
    Horticulture = 6,
    FisheriesPoultry = 7
}

public enum AgriculturalLoanStatus
{
    Initiated = 1,
    Disbursed = 2,
    Active = 3,
    RescheduledNonAccrual = 4,
    Closed = 5,
    Defaulted = 6
}

public enum BillDiscountingStatus
{
    Discounted = 1,
    MaturedPendingRealization = 2,
    RealizedDraweePaid = 3,
    EarlyRetiredRebated = 4,
    Defaulted = 5
}

public enum InterBranchInstrumentType
{
    DemandDraft = 1,
    TelegraphicTransfer = 2
}

public enum InterBranchInstrumentStatus
{
    IssuedPendingAdvice = 1,
    AdviceReceived = 2,
    PaidSettled = 3,
    CancelledReversed = 4,
    BlockedSecurityAlert = 5
}
