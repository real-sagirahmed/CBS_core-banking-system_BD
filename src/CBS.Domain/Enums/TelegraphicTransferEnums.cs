// ARCHITECTURAL INTENT: Telegraphic/Telephonic Transfer (TT) Enums
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 3 (3.13) and Module 1 (1.6.5).

namespace CBS.Domain.Enums;

public enum TtStatus
{
    Issued = 1,
    AdviceTransmitted = 2,
    TelephonicallyVerified = 3,
    Responded = 4,
    Cancelled = 5
}

public enum TtCommissionSlab
{
    UpTo25K = 1,
    From25KTo100K = 2,
    From100KTo500K = 3,
    From500KTo1M = 4,
    Above1M = 5
}
