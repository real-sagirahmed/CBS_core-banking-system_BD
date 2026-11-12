using System;

namespace CBS.Domain.Enums;

// ARCHITECTURAL INTENT: CBS Transaction Governance, PAM Emergency Break-Glass & EOD Balancing Enums
// (BRPD Circular 12/2024 Modules 9, 10 & 11).

public enum AnomalySuspicionType
{
    SmurfingStructuring = 1,                 // Multiple transactions aggregate to >= 1M BDT CTR threshold
    EmployeeSelfAccountTransaction = 2,      // Teller/Officer posting to own personal/signatory account
    OffHoursPostingWithoutOverride = 3,      // Financial posting outside branch operational hours without DOA
    VelocitySurgeHighRisk = 4                // Sudden burst of high-frequency debits/credits
}

public enum AnomalyResolutionStatus
{
    DetectedPendingInvestigation = 1,
    ClearedLegitimateBusiness = 2,
    EscalatedToBfiuStr = 3
}

public enum BreakGlassStatus
{
    RequestedPendingApproval = 1,
    PartiallyApprovedFirstOfficer = 2,
    ApprovedActive = 3,
    Expired = 4,
    Revoked = 5,
    Rejected = 6
}

public enum EodProofingStatus
{
    BalancedInProof = 1,
    DiscrepancyDetected = 2,
    Quarantined = 3
}
