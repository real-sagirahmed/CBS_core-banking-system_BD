using System;

namespace CBS.Domain.Enums;

// ARCHITECTURAL INTENT: CRM Corporate Group Hierarchy, UBO, TP & ADC Reconciliation Enums (BRPD Circular 12/2024 Modules 7 & 8).
// Preserves regulatory definitions for Corporate Group structure, BFIU Beneficial Ownership thresholds,
// Customer Transaction Profile monitoring categories, and Central ADC Multi-Switch Settlement reconciliation.

public enum CustomerGroupRole
{
    ParentHoldingCompany = 1,
    Subsidiary = 2,
    SisterConcern = 3,
    DirectorSignatory = 4,
    CorporateGuarantor = 5,
    AssociateCompany = 6
}

public enum BeneficialOwnerType
{
    NaturalPersonMajorityShareholder = 1, // >= 25% voting shares per BFIU AML/CFT rules
    EffectiveControlExerciser = 2,       // Controlling mind through management contracts / board powers
    SeniorManagingOfficial = 3           // CEO / MD acting as fallback controller
}

public enum TpTransactionCategory
{
    CashDeposit = 1,
    CashWithdrawal = 2,
    ClearingTransferDeposit = 3,
    ClearingTransferWithdrawal = 4,
    RemittanceInward = 5,
    RemittanceOutward = 6,
    AdcDigitalDebit = 7,
    AdcDigitalCredit = 8
}

public enum AdcSettlementNetwork
{
    NPSB = 1,
    VISA = 2,
    MasterCard = 3,
    MFS_bKash = 4,
    MFS_Nagad = 5,
    MFS_Rocket = 6,
    Binimoy_IDTP = 7
}

public enum ReconciliationMatchStatus
{
    Matched = 1,
    Mismatched_ExcessInSwitch = 2,   // Debited customer at switch/ATM but not recognized/completed in CBS
    Mismatched_MissingInSwitch = 3,  // Debited in CBS but rejected/not found in Switch file
    AmountMismatch = 4               // Transaction found in both but amounts differ
}

public enum ReconciliationBatchStatus
{
    Open = 1,
    Processing = 2,
    Reconciled = 3,
    DiscrepancyPendingRectification = 4,
    ClosedWithRectifications = 5
}

public enum RectificationAction
{
    CreditCustomerDebitSettlementSuspense = 1, // Refund customer for failed ATM cashout / excess switch debit
    DebitCustomerCreditSettlementSuspense = 2, // Recover from customer if switch confirmed cash dispense without CBS debit
    ManualInvestigationRequired = 3           // Disputed / Fraud claim requiring physical audit
}
