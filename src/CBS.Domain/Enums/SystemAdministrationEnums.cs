using System;

namespace CBS.Domain.Enums;

// ARCHITECTURAL INTENT: Delegation of Authority (DOA), Temporary Acting Delegation & Password Lifecycle Enums
// (BRPD Circular 12/2024 Module 10: System Administration).

public enum TransactionLimitType
{
    LocalDebit = 1,
    LocalCredit = 2,
    InterBranchDebit = 3,
    InterBranchCredit = 4,
    GlManualAdjustment = 5
}

public enum DelegationStatus
{
    PendingApproval = 1,
    Active = 2,
    Expired = 3,
    Revoked = 4,
    Rejected = 5
}
