namespace CBS.Domain.Enums;

public enum AdcChannelType
{
    Atm = 1,
    Crm = 2,
    Pos = 3,
    InternetBanking = 4,
    MobileApp = 5,
    Mfs = 6,
    BinimoyIdtp = 7,
    CallCenter = 8
}

public enum AdcTransactionStatus
{
    Pending = 1,
    Approved = 2,
    Declined = 3,
    Reversed = 4,
    Disputed = 5,
    Reconciled = 6
}

public enum ReconciliationStatus
{
    Pending = 1,
    Matched = 2,
    DiscrepancyDetected = 3,
    Settled = 4
}

public enum DisputeStatus
{
    Logged = 1,
    UnderInvestigation = 2,
    RefundApproved = 3,
    Rejected = 4
}

public enum ReconAgingBucket
{
    Days1To7 = 1,
    Days8To15 = 2,
    Days16To30 = 3,
    Over30Days = 4
}
