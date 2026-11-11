namespace CBS.Domain.Enums;

// ARCHITECTURAL INTENT: Enums governing Bank Guarantees (Module 5.4) and
// Multi-Channel Reconciliation Aging & Provisioning (Module 1.4) conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024).
public enum GuaranteeType
{
    BidBond_Tender = 1,
    PerformanceBond = 2,
    AdvancePaymentGuarantee = 3,
    FinancialGuarantee = 4,
    CustomsGuarantee = 5
}

public enum BankGuaranteeStatus
{
    Issued = 1,
    Amended = 2,
    ClaimInvoked = 3,
    LiquidatedClaimPaid = 4,
    CancelledReleased = 5,
    Expired = 6
}

public enum GuaranteeExpiryType
{
    FixedDate = 1,
    OpenEndedWithClaimPeriod = 2
}

public enum ReconciliationAgingBucket
{
    Days0To30 = 1,      // 0% Statutory provision
    Days31To60 = 2,     // 10% Statutory provision
    Days61To90 = 3,     // 25% Statutory provision
    Days91To180 = 4,    // 50% Statutory provision
    Over180Days = 5     // 100% Statutory provision
}
