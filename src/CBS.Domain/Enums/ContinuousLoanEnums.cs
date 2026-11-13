// ARCHITECTURAL INTENT: Enums for Continuous Loan (Cash Credit / Overdraft - CC/OD) Facilities
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 and 3.9.

namespace CBS.Domain.Enums;

public enum ContinuousLoanCategory
{
    CashCredit_Hypothecation = 1,
    CashCredit_Pledge = 2,
    SecuredOverdraft_Financial = 3,  // SOD against FDR, Wage Earner Bond, Treasury Bond
    SecuredOverdraft_General = 4,    // SOD against receivables, property, general business assets
    CleanOverdraft = 5              // Unsecured OD for high-net-worth commercial borrowers
}

public enum ContinuousLoanRenewalStatus
{
    Active = 1,
    Expired = 2,
    Renewed = 3,
    UnderRenewalReview = 4
}

public enum StockInspectionResult
{
    Satisfactory = 1,
    DeficientStock = 2,
    InadequateInsurance = 3,
    DiscrepantBooks = 4
}

public enum TodStatus
{
    Active = 1,
    Expired = 2,
    Liquidated = 3
}
