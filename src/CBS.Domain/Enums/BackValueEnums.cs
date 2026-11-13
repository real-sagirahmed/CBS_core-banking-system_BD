using System;

namespace CBS.Domain.Enums;

// ARCHITECTURAL INTENT: Enums governing Back-Value Dated Transactions, Retroactive Interest Differential Adjustment
// and Linked Cascade Reversals per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 1 (1.3.4 & 1.3.6).

public enum BackValueStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Executed = 4
}

public enum BackValueTargetType
{
    DepositAccount = 1,
    LoanAccount = 2,
    GeneralLedgerVoucher = 3
}

public enum AdjustmentDirection
{
    CreditCustomer = 1,
    DebitCustomer = 2
}

public enum ReversalType
{
    ParentTransaction = 1,
    LinkedServiceCharge = 2,
    LinkedVat = 3,
    LinkedExciseDuty = 4
}
