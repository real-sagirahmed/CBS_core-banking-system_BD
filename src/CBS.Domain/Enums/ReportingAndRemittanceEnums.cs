using System;

namespace CBS.Domain.Enums;

// ARCHITECTURAL INTENT: Enterprise Reporting & 2.5% Remittance Cash Incentive Enums
// (BRPD Circular 12/2024 Module 11, FE Circular 01/2022).

public enum RemittanceIncentiveClaimStatus
{
    PendingClaimSubmission = 1,
    ClaimSubmittedToCentralBank = 2,
    ReimbursedByCentralBank = 3
}

public enum CleanCashProofStatus
{
    BalancedInProof = 1,
    DiscrepancyDetected = 2
}
