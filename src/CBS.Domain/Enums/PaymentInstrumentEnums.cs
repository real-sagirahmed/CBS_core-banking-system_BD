// ARCHITECTURAL INTENT: Payment Instrument Enums governing Payment Orders (PO) and Demand Drafts (DD)
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 1 (1.5.6) & Module 3 (3.11, 3.12).

namespace CBS.Domain.Enums;

public enum PaymentInstrumentType
{
    PayOrder = 1,
    DemandDraft = 2
}

public enum PaymentInstrumentStatus
{
    InStock = 1,
    Issued = 2,
    Paid = 3,
    Cancelled = 4,
    Stopped = 5,
    DuplicateIssued = 6
}

public enum InstrumentSlabType
{
    UpTo25K = 1,
    From25KTo100K = 2,
    From100KTo500K = 3,
    From500KTo1M = 4,
    Above1M = 5
}
