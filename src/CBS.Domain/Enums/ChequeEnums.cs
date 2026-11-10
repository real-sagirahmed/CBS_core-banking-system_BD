namespace CBS.Domain.Enums;

public enum ChequeBookStatus
{
    Requested = 1,
    Printed = 2,
    DispatchedToBranch = 3,
    DeliveredToCustomer = 4,
    Destroyed = 5
}

public enum ChequeLeafStatus
{
    Available = 1,
    Presented = 2,
    Paid = 3,
    Stopped = 4,
    Cancelled = 5,
    Returned = 6
}

public enum StopPaymentReason
{
    LostOrStolen = 1,
    ChequeDamaged = 2,
    CommercialDispute = 3,
    ForgedSignatureSuspected = 4,
    CustomerRequest = 5
}
